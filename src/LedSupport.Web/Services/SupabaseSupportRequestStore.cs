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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

    public async Task<string> SaveAsync(SupportRequestDto request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = new
        {
            name = request.Name,
            email = request.Email,
            phone = request.Phone,
            company = request.Company,
            system = request.System,
            subject = request.Subject,
            message = request.Message,
            client_ip = request.ClientIp,
            user_agent = request.UserAgent,
            status = "new",
            email_sent = false
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "rest/v1/support_messages")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyHeaders(message);
        message.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Supabase insert failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Supabase support_messages insert failed.");
        }

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
            ? doc.RootElement[0].GetProperty("id").GetString()
            : null;

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Supabase insert returned no id.");
        }

        _logger.LogInformation("Support request saved to Supabase {Id}", id);
        return id;
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

        var payload = new
        {
            status = emailSent ? "email_sent" : "email_failed",
            email_sent = emailSent,
            email_error = emailError
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
                body);
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:ServiceRoleKey via environment variables.");
        }

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
}
