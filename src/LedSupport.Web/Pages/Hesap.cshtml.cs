using System.Security.Claims;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages;

[Authorize]
public class HesapModel : PageModel
{
    private readonly ISupabaseAccountService _accounts;

    public HesapModel(ISupabaseAccountService accounts)
    {
        _accounts = accounts;
    }

    public AppUser? Profile { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        Profile = await _accounts.GetProfileAsync(userId, cancellationToken);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToPage("/Giris");
        }

        try
        {
            await _accounts.DeleteAccountAsync(userId, cancellationToken);
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Index");
        }
        catch (Exception)
        {
            Profile = await _accounts.GetProfileAsync(userId, cancellationToken);
            ErrorMessage = "Hesap silinemedi. Lütfen daha sonra tekrar deneyin.";
            return Page();
        }
    }
}
