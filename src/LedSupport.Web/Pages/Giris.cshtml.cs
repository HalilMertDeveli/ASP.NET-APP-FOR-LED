using System.Text.Json;
using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class GirisModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISupabaseAccountService _accounts;
    private readonly SupabaseSettings _supabase;

    public GirisModel(ISupabaseAccountService accounts, IOptions<SupabaseSettings> supabase)
    {
        _accounts = accounts;
        _supabase = supabase.Value;
    }

    public string SupabaseUrl => _supabase.Url?.TrimEnd('/') ?? "";
    public string PublishableKey => _supabase.PublishableKey;
    public bool IsConfigured => _supabase.HasPublicClient;
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPanel();
        }

        if (!IsConfigured)
        {
            ErrorMessage = "Google girişi henüz yapılandırılmadı. Supabase URL ve anon anahtarı eksik.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken cancellationToken)
    {
        SessionPayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<SessionPayload>(Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Oturum bilgisi okunamadı." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return BadRequest(new { error = "Google oturumu bulunamadı." });
        }

        var user = await _accounts.VerifyAccessTokenAsync(payload.AccessToken, cancellationToken);
        if (user is null)
        {
            return new JsonResult(new { error = "Google oturumu doğrulanamadı." }) { StatusCode = 401 };
        }

        await AuthCookieService.SignInAsync(HttpContext, user, payload.AccessToken, payload.RefreshToken ?? "");
        return new JsonResult(new { redirect = user.Role == "admin" ? "/Admin" : "/Hesap" });
    }

    private IActionResult RedirectToPanel() =>
        User.IsInRole("admin") ? RedirectToPage("/Admin/Index") : RedirectToPage("/Hesap");

    private sealed class SessionPayload
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }
}
