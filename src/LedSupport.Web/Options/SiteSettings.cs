namespace LedSupport.Web.Options;

public sealed class SiteSettings
{
    public const string SectionName = "Site";

    public string CompanyName { get; set; } = "Led Teknik Destek";
    public string Tagline { get; set; } = "LED Ekran Teknik Destek";
    public string Email { get; set; } = "halilmertdeveliii@gmail.com";
    public string Phone { get; set; } = "+90 542 519 2119";
    public string PhoneHref { get; set; } = "tel:+905425192119";
    public string WhatsAppUrl { get; set; } = "https://wa.me/905425192119";
    public string BaseUrl { get; set; } = "https://localhost:5052";
    public string DefaultOgImage { get; set; } = "/images/og-default.svg";
}

/// <summary>
/// Direct = ASP.NET writes Supabase support_messages + sends Resend email.
/// </summary>
public sealed class SupportSettings
{
    public const string SectionName = "Support";

    public string Mode { get; set; } = "Direct";

    /// <summary>When true, Supabase write failure fails the whole request.</summary>
    public bool RequireStore { get; set; } = true;

    /// <summary>Legacy env alias Support__RequireFirestore.</summary>
    public bool RequireFirestore
    {
        get => RequireStore;
        set => RequireStore = value;
    }

    public int RateLimitPerWindow { get; set; } = 3;
    public int RateLimitWindowMinutes { get; set; } = 15;
}

public sealed class ResendSettings
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "LED Teknik Destek <onboarding@resend.dev>";
    public string ToEmail { get; set; } = "halilmertdeveliii@gmail.com";
}

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Led Teknik Destek";
    public string ToEmail { get; set; } = string.Empty;
}
