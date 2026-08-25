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
/// Direct = ASP.NET writes Firestore (optional) + sends Resend email.
/// Function = ASP.NET posts to Cloud Function (requires Blaze + deployed function).
/// </summary>
public sealed class SupportSettings
{
    public const string SectionName = "Support";

    /// <summary>Direct | Function</summary>
    public string Mode { get; set; } = "Direct";

    /// <summary>When true, Firestore write failure fails the whole request.</summary>
    public bool RequireFirestore { get; set; } = true;

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

public sealed class FirebaseSettings
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = "ledteknikdestek-1e74e";

    /// <summary>Public web API key (safe to expose).</summary>
    public string WebApiKey { get; set; } = string.Empty;

    public string AuthDomain { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to service account JSON (user-secrets / env). Never commit this file.
    /// </summary>
    public string CredentialsPath { get; set; } = string.Empty;
}

public sealed class FirebaseSupportSettings
{
    public const string SectionName = "FirebaseSupport";

    public string SubmitUrl { get; set; } = string.Empty;
    public string IngestSecret { get; set; } = string.Empty;
    public int RateLimitPerWindow { get; set; } = 3;
    public int RateLimitWindowMinutes { get; set; } = 15;
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
