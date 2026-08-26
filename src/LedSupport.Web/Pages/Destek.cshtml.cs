using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Pages;

[Authorize]
public class DestekModel : PageModel
{
    private readonly IChatStore _chat;
    private readonly ICustomerRequestStore _requests;
    private readonly ISupabaseAccountService _accounts;
    private readonly IResendEmailService _email;
    private readonly ILogger<DestekModel> _logger;
    private readonly SupabaseSettings _supabase;

    public DestekModel(
        IChatStore chat,
        ICustomerRequestStore requests,
        ISupabaseAccountService accounts,
        IResendEmailService email,
        IOptions<SupabaseSettings> supabase,
        ILogger<DestekModel> logger)
    {
        _chat = chat;
        _requests = requests;
        _accounts = accounts;
        _email = email;
        _supabase = supabase.Value;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid RequestId { get; set; }

    public CustomerRequestRecord? RequestItem { get; private set; }
    public Guid ConversationId { get; private set; }
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
    public int UnreadCount { get; private set; }
    public string SupabaseUrl => _supabase.Url?.TrimEnd('/') ?? "";
    public string PublishableKey => _supabase.PublishableKey;
    public string AccessToken { get; private set; } = "";
    public string? ErrorMessage { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (RequestId == Guid.Empty)
        {
            return RedirectToPage("/Talepler");
        }

        if (!await TryOpenAsync(cancellationToken))
        {
            return Page();
        }

        UnreadCount = await _chat.CountUnreadAsync(ConversationId, "customer", cancellationToken);
        await _chat.MarkReadAsync(ConversationId, "customer", cancellationToken);
        Messages = await _chat.ListMessagesAsync(ConversationId, cancellationToken);
        AccessToken = await ReadAccessTokenAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await TryOpenAsync(cancellationToken))
        {
            return Page();
        }

        if (!ModelState.IsValid)
        {
            Messages = await _chat.ListMessagesAsync(ConversationId, cancellationToken);
            AccessToken = await ReadAccessTokenAsync();
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var sent = await _chat.SendAsync(ConversationId, userId, "customer", Input.Body.Trim(), cancellationToken);

        try
        {
            var profile = await _accounts.GetProfileAsync(userId, cancellationToken);
            await _email.SendChatNotificationEmailAsync(
                profile?.FullName ?? User.Identity?.Name ?? "Müşteri",
                profile?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? "",
                $"{RequestItem?.Subject}\n\n{sent.Body}",
                ConversationId.ToString(),
                sent.CreatedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat notification email failed for {Conversation}", ConversationId);
        }

        return RedirectToPage(new { requestId = RequestId });
    }

    private async Task<bool> TryOpenAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        try
        {
            RequestItem = await _requests.GetAsync(RequestId, cancellationToken);
            if (RequestItem is null || !string.Equals(RequestItem.CustomerId, userId, StringComparison.Ordinal))
            {
                ErrorMessage = "Bu talebe erişemezsiniz.";
                return false;
            }

            var conversation = await _chat.GetByRequestAsync(RequestId, cancellationToken);
            if (conversation is null)
            {
                ErrorMessage = "Konuşma bulunamadı.";
                return false;
            }

            ConversationId = conversation.Id;
            AccessToken = await ReadAccessTokenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open customer conversation");
            ErrorMessage = "Destek sohbeti açılamadı. Lütfen daha sonra tekrar deneyin.";
            return false;
        }
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
