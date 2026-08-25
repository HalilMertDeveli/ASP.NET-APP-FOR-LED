using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public interface IResendEmailService
{
    Task SendSupportRequestEmailAsync(
        SupportRequestDto request,
        string requestId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}

public sealed class ResendEmailService : IResendEmailService
{
    private readonly HttpClient _http;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient http,
        IOptions<ResendSettings> settings,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
        _http.BaseAddress ??= new Uri("https://api.resend.com/");
    }

    public async Task SendSupportRequestEmailAsync(
        SupportRequestDto request,
        string requestId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            _settings.ApiKey.Contains("YOUR_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resend:ApiKey is not configured (user-secrets / env).");
        }

        var text = new StringBuilder()
            .AppendLine("Yeni LED teknik destek talebi")
            .AppendLine("----------------------------------------")
            .AppendLine($"Talep ID     : {requestId}")
            .AppendLine($"Talep tarihi : {createdAt:yyyy-MM-dd HH:mm:ss} UTC")
            .AppendLine($"Ad Soyad     : {request.Name}")
            .AppendLine($"Firma adı    : {request.Company ?? "-"}")
            .AppendLine($"E-posta      : {request.Email}")
            .AppendLine($"Telefon      : {request.Phone ?? "-"}")
            .AppendLine($"Sistem       : {request.System}")
            .AppendLine($"Konu         : {request.Subject}")
            .AppendLine()
            .AppendLine("Sorun açıklaması:")
            .AppendLine(request.Message)
            .AppendLine("----------------------------------------")
            .ToString();

        using var message = new HttpRequestMessage(HttpMethod.Post, "emails");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        message.Content = JsonContent.Create(new
        {
            from = _settings.FromEmail,
            to = new[] { _settings.ToEmail },
            reply_to = request.Email,
            subject = $"[LED Teknik Destek] Yeni Talep - {request.Subject}",
            text
        });

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend API failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Resend email delivery failed.");
        }
    }
}
