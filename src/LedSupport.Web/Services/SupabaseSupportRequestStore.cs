using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public sealed class SupabaseSupportRequestStore : ISupportRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// PostgREST matches RPC overloads by present JSON keys. Nulls must be serialized
    /// (not omitted) so p_secret and optional args still appear in the payload.
    /// </summary>
    private static readonly JsonSerializerOptions RpcJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;
    private readonly SupportSettings _support;
    private readonly ILogger<SupabaseSupportRequestStore> _logger;

    public SupabaseSupportRequestStore(
        HttpClient http,
        IOptions<SupabaseSettings> settings,
        IOptions<SupportSettings> support,
        ILogger<SupabaseSupportRequestStore> logger)
    {
        _http = http;
        _settings = settings.Value;
        _support = support.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        HasServiceRoleAuth || HasIngestAuth;

    private bool HasServiceRoleAuth =>
        !string.IsNullOrWhiteSpace(_settings.Url) &&
        !_settings.Url.Contains("YOUR_", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(_settings.ServiceRoleKey) &&
        !_settings.ServiceRoleKey.Contains("YOUR_", StringComparison.Ordinal);

    private bool HasIngestAuth =>
        !string.IsNullOrWhiteSpace(_settings.Url) &&
        !_settings.Url.Contains("YOUR_", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(_settings.PublishableKey) &&
        !_settings.PublishableKey.Contains("YOUR_", StringComparison.Ordinal);

    /// <summary>
    /// Prefer SECURITY DEFINER ingest RPCs when the publishable key is available.
    /// Avoids mismatched service_role JWTs from another Supabase project on Vercel.
    /// </summary>
    private bool UseIngestRpc => HasIngestAuth;

    /// <summary>
    /// Empty string (not null): JsonSerializer WhenWritingNull would omit null and
    /// PostgREST would fail to match submit_support_message overload requiring p_secret.
    /// </summary>
    private static string IngestSecretOrEmpty => string.Empty;

    public async Task<SupportMessageSaveResult> SaveAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (UseIngestRpc)
        {
            return await SaveViaIngestRpcAsync(request, cancellationToken);
        }

        if (request.IdempotencyKey is { } key && key != Guid.Empty)
        {
            var existing = await FindByIdempotencyKeyAsync(key, cancellationToken);
            if (existing is not null)
            {
                return existing with { AlreadyPersisted = true };
            }
        }

        var payload = new
        {
            idempotency_key = request.IdempotencyKey == Guid.Empty ? (Guid?)null : request.IdempotencyKey,
            name = request.Name,
            email = request.Email,
            phone = request.Phone,
            company = request.Company,
            system = request.System,
            subject = request.Subject,
            message = request.Message,
            client_ip = Truncate(request.ClientIp, 64),
            user_agent = Truncate(request.UserAgent, 512),
            email_status = "pending"
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "rest/v1/support_messages")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyHeaders(message);
        message.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if ((int)response.StatusCode == 409 && request.IdempotencyKey is { } dup && dup != Guid.Empty)
        {
            var conflicted = await FindByIdempotencyKeyAsync(dup, cancellationToken);
            if (conflicted is not null)
            {
                return conflicted with { AlreadyPersisted = true };
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Supabase insert failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 500));
            throw new InvalidOperationException("Supabase support_messages insert failed.");
        }

        using var doc = JsonDocument.Parse(body);
        var id = ReadId(doc.RootElement);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Supabase insert returned no id.");
        }

        _logger.LogInformation("Support request saved to Supabase {Id}", id);
        return new SupportMessageSaveResult
        {
            Id = id,
            EmailStatus = "pending",
            AlreadyPersisted = false
        };
    }

    public async Task MarkEmailResultAsync(
        string requestId,
        bool emailSent,
        string? emailError,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(requestId) || !Guid.TryParse(requestId, out var id))
        {
            return;
        }

        EnsureBaseAddress();

        if (UseIngestRpc)
        {
            using var rpc = new HttpRequestMessage(HttpMethod.Post, "rest/v1/rpc/mark_support_email_result")
            {
                Content = JsonContent.Create(new
                {
                    p_secret = IngestSecretOrEmpty,
                    p_id = id,
                    p_email_sent = emailSent,
                    p_email_error = Truncate(emailError, 500)
                }, options: RpcJsonOptions)
            };
            ApplyHeaders(rpc);
            using var rpcResponse = await _http.SendAsync(rpc, cancellationToken);
            if (!rpcResponse.IsSuccessStatusCode)
            {
                var body = await rpcResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Supabase email status RPC failed for {Id}: {Status} {Body}",
                    requestId,
                    (int)rpcResponse.StatusCode,
                    Truncate(body, 500));
            }

            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["email_status"] = emailSent ? "sent" : "failed",
            ["email_sent_at"] = emailSent ? DateTimeOffset.UtcNow : null,
            ["error_message"] = emailSent ? null : Truncate(emailError, 500)
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/support_messages?id=eq.{Uri.EscapeDataString(requestId)}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyHeaders(message);
        message.Headers.TryAddWithoutValidation("Prefer", "return=minimal");

        using var response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Supabase email status update failed for {Id}: {Status} {Body}",
                requestId,
                (int)response.StatusCode,
                Truncate(body, 500));
        }
    }

    public async Task<EmailSendClaimResult> TryClaimEmailSendAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(requestId) || !Guid.TryParse(requestId, out var id))
        {
            return EmailSendClaimResult.Failed;
        }

        EnsureBaseAddress();

        if (UseIngestRpc)
        {
            using var rpc = new HttpRequestMessage(HttpMethod.Post, "rest/v1/rpc/claim_support_email_send")
            {
                Content = JsonContent.Create(new
                {
                    p_secret = IngestSecretOrEmpty,
                    p_id = id
                }, options: RpcJsonOptions)
            };
            ApplyHeaders(rpc);
            using var rpcResponse = await _http.SendAsync(rpc, cancellationToken);
            var body = await rpcResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!rpcResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Supabase email claim RPC failed for {Id}: {Status} {Body}",
                    requestId,
                    (int)rpcResponse.StatusCode,
                    Truncate(body, 500));
                return EmailSendClaimResult.Failed;
            }

            var result = body.Trim().Trim('"');
            return string.Equals(result, "claimed", StringComparison.OrdinalIgnoreCase)
                ? EmailSendClaimResult.Claimed
                : EmailSendClaimResult.AlreadyHandled;
        }

        var payload = new Dictionary<string, object?>
        {
            ["email_status"] = "sending",
            ["error_message"] = null
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/support_messages?id=eq.{Uri.EscapeDataString(requestId)}&email_status=in.(pending,failed)")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyHeaders(message);
        message.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        using var response = await _http.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase email claim failed for {Id}: {Status} {Body}",
                requestId,
                (int)response.StatusCode,
                Truncate(responseBody, 500));
            return EmailSendClaimResult.Failed;
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            return EmailSendClaimResult.Claimed;
        }

        return EmailSendClaimResult.AlreadyHandled;
    }

    private async Task<SupportMessageSaveResult> SaveViaIngestRpcAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureBaseAddress();

        using var message = new HttpRequestMessage(HttpMethod.Post, "rest/v1/rpc/submit_support_message")
        {
            Content = JsonContent.Create(new
            {
                p_secret = IngestSecretOrEmpty,
                p_idempotency_key = request.IdempotencyKey == Guid.Empty ? (Guid?)null : request.IdempotencyKey,
                p_name = request.Name,
                p_email = request.Email,
                p_phone = request.Phone,
                p_company = request.Company,
                p_system = request.System,
                p_subject = request.Subject,
                p_message = request.Message,
                p_client_ip = Truncate(request.ClientIp, 64),
                p_user_agent = Truncate(request.UserAgent, 512)
            }, options: RpcJsonOptions)
        };
        ApplyHeaders(message);

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Supabase ingest RPC failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 500));
            throw new InvalidOperationException("Supabase support_messages insert failed.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var status = root.TryGetProperty("email_status", out var st) ? st.GetString() : "pending";
        var already = root.TryGetProperty("already_persisted", out var ap) && ap.ValueKind == JsonValueKind.True;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Supabase ingest RPC returned no id.");
        }

        _logger.LogInformation("Support request saved via ingest RPC {Id}", id);
        return new SupportMessageSaveResult
        {
            Id = id,
            EmailStatus = status ?? "pending",
            AlreadyPersisted = already
        };
    }

    private async Task<SupportMessageSaveResult?> FindByIdempotencyKeyAsync(
        Guid key,
        CancellationToken cancellationToken)
    {
        EnsureBaseAddress();

        if (UseIngestRpc)
        {
            using var rpc = new HttpRequestMessage(HttpMethod.Post, "rest/v1/rpc/find_support_message_by_idempotency")
            {
                Content = JsonContent.Create(new
                {
                    p_secret = IngestSecretOrEmpty,
                    p_idempotency_key = key
                }, options: RpcJsonOptions)
            };
            ApplyHeaders(rpc);
            using var rpcResponse = await _http.SendAsync(rpc, cancellationToken);
            if (!rpcResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await rpcResponse.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body) || body == "null")
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var status = doc.RootElement.TryGetProperty("email_status", out var st) ? st.GetString() : "pending";
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return new SupportMessageSaveResult
            {
                Id = id,
                EmailStatus = status ?? "pending",
                AlreadyPersisted = true
            };
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/support_messages?idempotency_key=eq.{key:D}&select=id,email_status&limit=1");
        ApplyHeaders(message);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var arrayDoc = JsonDocument.Parse(responseBody);
        if (arrayDoc.RootElement.ValueKind != JsonValueKind.Array || arrayDoc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var row = arrayDoc.RootElement[0];
        var rowId = row.TryGetProperty("id", out var rowIdProp) ? rowIdProp.GetString() : null;
        var rowStatus = row.TryGetProperty("email_status", out var rowSt) ? rowSt.GetString() : "pending";
        if (string.IsNullOrWhiteSpace(rowId))
        {
            return null;
        }

        return new SupportMessageSaveResult
        {
            Id = rowId,
            EmailStatus = rowStatus ?? "pending",
            AlreadyPersisted = true
        };
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url with either ServiceRoleKey or PublishableKey.");
        }

        EnsureBaseAddress();
    }

    private void EnsureBaseAddress()
    {
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(_settings.Url.TrimEnd('/') + "/");
        }
    }

    private void ApplyHeaders(HttpRequestMessage message)
    {
        var key = UseIngestRpc ? _settings.PublishableKey : _settings.ServiceRoleKey;
        message.Headers.TryAddWithoutValidation("apikey", key);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    private static string? ReadId(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            return root[0].TryGetProperty("id", out var id) ? id.GetString() : null;
        }

        return root.TryGetProperty("id", out var direct) ? direct.GetString() : null;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }
}
