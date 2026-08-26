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

    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;
    private readonly ILogger<SupabaseSupportRequestStore> _logger;

    public SupabaseSupportRequestStore(
        HttpClient http,
        IOptions<SupabaseSettings> settings,
        ILogger<SupabaseSupportRequestStore> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.Url) &&
        !_settings.Url.Contains("YOUR_", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(_settings.ServiceRoleKey) &&
        !_settings.ServiceRoleKey.Contains("YOUR_", StringComparison.Ordinal);

    public async Task<SupportMessageSaveResult> SaveAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

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
        if (!IsConfigured || string.IsNullOrWhiteSpace(requestId) || !Guid.TryParse(requestId, out _))
        {
            return;
        }

        EnsureBaseAddress();

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
        if (!IsConfigured || string.IsNullOrWhiteSpace(requestId) || !Guid.TryParse(requestId, out _))
        {
            return EmailSendClaimResult.Failed;
        }

        EnsureBaseAddress();

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
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase email claim failed for {Id}: {Status} {Body}",
                requestId,
                (int)response.StatusCode,
                Truncate(body, 500));
            return EmailSendClaimResult.Failed;
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            return EmailSendClaimResult.Claimed;
        }

        return EmailSendClaimResult.AlreadyHandled;
    }

    private async Task<SupportMessageSaveResult?> FindByIdempotencyKeyAsync(
        Guid key,
        CancellationToken cancellationToken)
    {
        EnsureBaseAddress();

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/support_messages?idempotency_key=eq.{key:D}&select=id,email_status&limit=1");
        ApplyHeaders(message);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var row = doc.RootElement[0];
        var id = row.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var status = row.TryGetProperty("email_status", out var st) ? st.GetString() : "pending";
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

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:ServiceRoleKey via environment variables.");
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
        message.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
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
