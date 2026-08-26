using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class KonusmaModel : PageModel
{
    private readonly IChatStore _chat;
    private readonly ICustomerRequestStore _requests;
    private readonly ISupabaseAccountService _accounts;
    private readonly SupabaseSettings _supabase;

    public KonusmaModel(
        IChatStore chat,
        ICustomerRequestStore requests,
        ISupabaseAccountService accounts,
        IOptions<SupabaseSettings> supabase)
    {
        _chat = chat;
        _requests = requests;
        _accounts = accounts;
        _supabase = supabase.Value;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public ConversationRecord? Conversation { get; private set; }
    public CustomerRequestRecord? RequestItem { get; private set; }
    public AppUser? Customer { get; private set; }
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
    public string SupabaseUrl => _supabase.Url?.TrimEnd('/') ?? "";
    public string PublishableKey => _supabase.PublishableKey;
    public string AccessToken { get; private set; } = "";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public string StatusValue { get; set; } = "open";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        if (!loaded)
        {
            return NotFound();
        }

        await _chat.MarkReadAsync(Id, "admin", cancellationToken);
        Messages = await _chat.ListMessagesAsync(Id, cancellationToken);
        AccessToken = await ReadAccessTokenAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        if (!loaded)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            Messages = await _chat.ListMessagesAsync(Id, cancellationToken);
            AccessToken = await ReadAccessTokenAsync();
            return Page();
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        await _chat.SendAsync(Id, adminId, "admin", Input.Body.Trim(), cancellationToken);
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostStatusAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        if (!loaded || RequestItem is null)
        {
            return NotFound();
        }

        await _requests.UpdateStatusAsync(RequestItem.Id, StatusValue, cancellationToken);
        return RedirectToPage(new { id = Id });
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Conversation = await _chat.GetAsync(Id, cancellationToken);
        if (Conversation is null)
        {
            return false;
        }

        Customer = await _accounts.GetProfileAsync(Conversation.CustomerId, cancellationToken);
        if (Conversation.RequestId is Guid requestId)
        {
            RequestItem = await _requests.GetAsync(requestId, cancellationToken);
            StatusValue = RequestItem?.Status ?? "open";
        }

        return true;
    }

    private async Task<string> ReadAccessTokenAsync()
    {
        var result = await HttpContext.AuthenticateAsync();
        return AuthCookieService.GetAccessToken(result) ?? "";
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Mesaj yazın")]
        [StringLength(4000, MinimumLength = 1)]
        public string Body { get; set; } = string.Empty;
    }
}
