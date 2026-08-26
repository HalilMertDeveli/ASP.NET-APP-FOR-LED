using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class MusteriModel : PageModel
{
    private readonly ISupabaseAccountService _accounts;
    private readonly ICustomerRequestStore _requests;

    public MusteriModel(ISupabaseAccountService accounts, ICustomerRequestStore requests)
    {
        _accounts = accounts;
        _requests = requests;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "";

    public AppUser? Customer { get; private set; }
    public IReadOnlyList<CustomerRequestRecord> Requests { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            return NotFound();
        }

        Customer = await _accounts.GetProfileAsync(Id, cancellationToken);
        if (Customer is null)
        {
            return NotFound();
        }

        Requests = await _requests.ListForCustomerAsync(Id, cancellationToken);
        return Page();
    }
}
