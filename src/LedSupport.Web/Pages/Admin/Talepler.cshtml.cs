using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class TaleplerModel : PageModel
{
    private readonly ICustomerRequestStore _requests;

    public TaleplerModel(ICustomerRequestStore requests)
    {
        _requests = requests;
    }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public IReadOnlyList<CustomerRequestListItem> Requests { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Requests = await _requests.ListForAdminAsync(Status, cancellationToken);
    }
}
