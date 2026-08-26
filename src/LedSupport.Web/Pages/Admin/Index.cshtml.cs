using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IChatStore _chat;
    private readonly ICustomerRequestStore _requests;

    public IndexModel(IChatStore chat, ICustomerRequestStore requests)
    {
        _chat = chat;
        _requests = requests;
    }

    public AdminDashboardStats Stats { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<ConversationListItem> Conversations { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Stats = await _requests.GetDashboardAsync(cancellationToken);
        Conversations = await _chat.ListForAdminAsync(cancellationToken);
    }
}
