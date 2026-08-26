using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IChatStore _chat;

    public IndexModel(IChatStore chat)
    {
        _chat = chat;
    }

    public IReadOnlyList<ConversationListItem> Conversations { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Conversations = await _chat.ListForAdminAsync(cancellationToken);
    }
}
