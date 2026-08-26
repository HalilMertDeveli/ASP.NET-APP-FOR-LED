using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

        var to = string.IsNullOrWhiteSpace(_settings.ToEmail)
            ? "halilmertdeveliii@gmail.com"
            : _settings.ToEmail.Trim();

        using var message = new HttpRequestMessage(HttpMethod.Post, "emails");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        message.Content = JsonContent.Create(new
        {
            from = _settings.FromEmail,
            to = new[] { to },
            reply_to = request.Email,
            subject = "LED Support - Yeni Destek Talebi",
            html = BuildHtml(request, requestId, createdAt),
            text = BuildText(request, requestId, createdAt)
        });

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend API failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Resend email delivery failed.");
        }
    }

    private static string BuildHtml(SupportRequestDto request, string requestId, DateTimeOffset createdAt)
    {
        static string E(string? value) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value);

        return $"""
            <h2>Yeni destek talebi</h2>
            <p><strong>Ad Soyad:</strong> {E(request.Name)}</p>
            <p><strong>E-posta:</strong> {E(request.Email)}</p>
            <p><strong>Telefon:</strong> {E(request.Phone)}</p>
            <p><strong>Firma:</strong> {E(request.Company)}</p>
            <p><strong>Sistem:</strong> {E(request.System)}</p>
            <p><strong>Konu:</strong> {E(request.Subject)}</p>
            <p><strong>Mesaj:</strong></p>
            <pre style="white-space:pre-wrap;font-family:inherit;">{E(request.Message)}</pre>
            <p><strong>Tarih:</strong> {E(createdAt.ToString("u"))}</p>
            <p><strong>Kayıt No:</strong> {E(requestId)}</p>
            """;
    }

    private static string BuildText(SupportRequestDto request, string requestId, DateTimeOffset createdAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Yeni destek talebi");
        sb.AppendLine();
        sb.AppendLine($"Ad Soyad: {request.Name}");
        sb.AppendLine($"E-posta: {request.Email}");
        sb.AppendLine($"Telefon: {request.Phone ?? "-"}");
        sb.AppendLine($"Firma: {request.Company ?? "-"}");
        sb.AppendLine($"Sistem: {request.System}");
        sb.AppendLine($"Konu: {request.Subject}");
        sb.AppendLine();
        sb.AppendLine("Mesaj:");
        sb.AppendLine(request.Message);
        sb.AppendLine();
        sb.AppendLine($"Tarih: {createdAt:u}");
        sb.AppendLine($"Kayıt No: {requestId}");
        return sb.ToString();
    }
}
