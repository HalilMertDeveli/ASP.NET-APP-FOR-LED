namespace LedSupport.Web.Options;

public sealed class SupabaseSettings
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Server-only service_role key. Never expose to the browser.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;
}
