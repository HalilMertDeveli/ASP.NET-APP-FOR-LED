using System.ComponentModel.DataAnnotations;
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
    private readonly ICustomerRequestStore _requests;

    public HesapModel(ISupabaseAccountService accounts, ICustomerRequestStore requests)
    {
        _accounts = accounts;
        _requests = requests;
    }

    public AppUser? Profile { get; private set; }
    public IReadOnlyList<CustomerRequestRecord> Requests { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostProfileAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToPage("/Giris");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            await _accounts.UpdateContactAsync(userId, Input.Phone, Input.Company, cancellationToken);
            SuccessMessage = "Profil güncellendi.";
        }
        catch (Exception)
        {
            ErrorMessage = "Profil güncellenemedi.";
        }

        await LoadAsync(cancellationToken);
        return Page();
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
            ErrorMessage = "Hesap silinemedi. Lütfen daha sonra tekrar deneyin.";
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        Profile = await _accounts.GetProfileAsync(userId, cancellationToken);
        Requests = await _requests.ListForCustomerAsync(userId, cancellationToken);
        Input.Phone = Profile?.Phone;
        Input.Company = Profile?.Company;
    }

    public sealed class ProfileInput
    {
        [Phone]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [StringLength(160)]
        [Display(Name = "Firma")]
        public string? Company { get; set; }
    }
}
