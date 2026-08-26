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

    Task SendChatNotificationEmailAsync(
        string customerName,
        string customerEmail,
        string message,
        string conversationId,
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

        var replyTo = SanitizeReplyTo(request.Email);

        using var message = new HttpRequestMessage(HttpMethod.Post, "emails");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        message.Content = JsonContent.Create(new
        {
            from = _settings.FromEmail,
            to = new[] { to },
            reply_to = replyTo,
            subject = "LED Support - Yeni Destek Talebi",
            html = BuildHtml(request, createdAt),
            text = BuildText(request, createdAt)
        });

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend API failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Resend email delivery failed.");
        }
    }

    public async Task SendChatNotificationEmailAsync(
        string customerName,
        string customerEmail,
        string message,
        string conversationId,
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

        static string E(string? value) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value);

        using var httpMessage = new HttpRequestMessage(HttpMethod.Post, "emails");
        httpMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        httpMessage.Content = JsonContent.Create(new
        {
            from = _settings.FromEmail,
            to = new[] { to },
            reply_to = SanitizeReplyTo(customerEmail),
            subject = "LED Support - Yeni müşteri mesajı",
            html = $"""
                <h2>Yeni müşteri mesajı</h2>
                <p><strong>Müşteri adı:</strong> {E(customerName)}</p>
                <p><strong>Müşteri e-posta:</strong> {E(customerEmail)}</p>
                <p><strong>Konuşma ID:</strong> {E(conversationId)}</p>
                <p><strong>Tarih:</strong> {E($"{createdAt:yyyy-MM-dd HH:mm} UTC")}</p>
                <p><strong>Mesaj:</strong></p>
                <pre style="white-space:pre-wrap;font-family:inherit;">{E(message)}</pre>
                """,
            text = $"Yeni müşteri mesajı\nAd: {customerName}\nE-posta: {customerEmail}\nKonuşma: {conversationId}\nTarih: {createdAt:yyyy-MM-dd HH:mm} UTC\n\n{message}"
        });

        using var response = await _http.SendAsync(httpMessage, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend chat notify failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Resend email delivery failed.");
        }
    }

    private static string BuildHtml(SupportRequestDto request, DateTimeOffset createdAt)
    {
        static string E(string? value) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value);

        return $"""
            <h2>Yeni Destek Talebi</h2>
            <p><strong>Ad Soyad:</strong> {E(request.Name)}</p>
            <p><strong>E-posta:</strong> {E(request.Email)}</p>
            <p><strong>Telefon:</strong> {E(request.Phone)}</p>
            <p><strong>Firma:</strong> {E(request.Company)}</p>
            <p><strong>Sistem:</strong> {E(request.System)}</p>
            <p><strong>Konu:</strong> {E(request.Subject)}</p>
            <p><strong>Mesaj:</strong></p>
            <pre style="white-space:pre-wrap;font-family:inherit;">{E(request.Message)}</pre>
            <p><strong>Tarih:</strong> {E($"{createdAt:yyyy-MM-dd HH:mm} UTC")}</p>
            """;
    }

    private static string BuildText(SupportRequestDto request, DateTimeOffset createdAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Yeni Destek Talebi");
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
        sb.AppendLine($"Tarih: {createdAt:yyyy-MM-dd HH:mm} UTC");
        return sb.ToString();
    }

    private static string? SanitizeReplyTo(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        if (trimmed.IndexOfAny(['\r', '\n', ',', ';']) >= 0)
        {
            return null;
        }

        return trimmed;
    }
}
