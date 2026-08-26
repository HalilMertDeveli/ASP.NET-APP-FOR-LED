namespace LedSupport.Web.Options;

public sealed class SupabaseSettings
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Public anon / publishable key. Safe to send to the browser.
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Server-only service_role key. Never expose to the browser.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    public bool HasPublicClient =>
        !string.IsNullOrWhiteSpace(Url) &&
        !Url.Contains("YOUR_", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(PublishableKey) &&
        !PublishableKey.Contains("YOUR_", StringComparison.Ordinal);

    public bool HasServiceRole =>
        !string.IsNullOrWhiteSpace(Url) &&
        !Url.Contains("YOUR_", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(ServiceRoleKey) &&
        !ServiceRoleKey.Contains("YOUR_", StringComparison.Ordinal);
}
