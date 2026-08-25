using System.Net;
using System.Net.Mail;
using System.Text;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _smtp;
    private readonly SiteSettings _site;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpSettings> smtp,
        IOptions<SiteSettings> site,
        ILogger<SmtpEmailSender> logger)
    {
        _smtp = smtp.Value;
        _site = site.Value;
        _logger = logger;
    }

    public async Task SendSupportRequestAsync(SupportRequestEmail request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) ||
            string.IsNullOrWhiteSpace(_smtp.ToEmail) ||
            string.IsNullOrWhiteSpace(_smtp.FromEmail))
        {
            throw new InvalidOperationException(
                "SMTP yapılandırması eksik. Site:Smtp (Host, FromEmail, ToEmail) ayarlarını tamamlayın.");
        }

        var subject = $"[Destek Talebi] {request.Subject} — {request.System}";
        var body = new StringBuilder()
            .AppendLine("Yeni LED teknik destek talebi")
            .AppendLine(new string('-', 40))
            .AppendLine($"Ad Soyad : {request.Name}")
            .AppendLine($"Firma    : {request.Company ?? "-"}")
            .AppendLine($"E-posta  : {request.Email}")
            .AppendLine($"Telefon  : {request.Phone ?? "-"}")
            .AppendLine($"Sistem   : {request.System}")
            .AppendLine($"Konu     : {request.Subject}")
            .AppendLine()
            .AppendLine("Sorun açıklaması:")
            .AppendLine(request.Message)
            .AppendLine()
            .AppendLine(new string('-', 40))
            .AppendLine($"Kaynak: {_site.CompanyName} web sitesi")
            .ToString();

        using var message = new MailMessage
        {
            From = new MailAddress(
                _smtp.FromEmail,
                string.IsNullOrWhiteSpace(_smtp.FromName) ? _site.CompanyName : _smtp.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(_smtp.ToEmail);
        message.ReplyToList.Add(new MailAddress(request.Email, request.Name));

        using var client = new SmtpClient(_smtp.Host, _smtp.Port)
        {
            EnableSsl = _smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_smtp.UserName))
        {
            client.Credentials = new NetworkCredential(_smtp.UserName, _smtp.Password);
        }

        _logger.LogInformation("Sending support request email to {To}", _smtp.ToEmail);
        await client.SendMailAsync(message, cancellationToken);
    }
}
