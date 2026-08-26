using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class MusterilerModel : PageModel
{
    private readonly ICustomerRequestStore _requests;

    public MusterilerModel(ICustomerRequestStore requests)
    {
        _requests = requests;
    }

    public IReadOnlyList<CustomerListItem> Customers { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Customers = await _requests.ListCustomersAsync(cancellationToken);
    }
}
