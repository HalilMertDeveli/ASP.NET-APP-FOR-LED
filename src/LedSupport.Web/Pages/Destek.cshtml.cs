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
    private readonly ISupabaseAccountService _accounts;
    private readonly IResendEmailService _email;
    private readonly ILogger<DestekModel> _logger;
    private readonly SupabaseSettings _supabase;

    public DestekModel(
        IChatStore chat,
        ISupabaseAccountService accounts,
        IResendEmailService email,
        IOptions<SupabaseSettings> supabase,
        ILogger<DestekModel> logger)
    {
        _chat = chat;
        _accounts = accounts;
        _email = email;
        _supabase = supabase.Value;
        _logger = logger;
    }

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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToPage("/Giris");
        }

        if (!await TryOpenConversationAsync(userId, cancellationToken))
        {
            AccessToken = await ReadAccessTokenAsync();
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToPage("/Giris");
        }

        if (!await TryOpenConversationAsync(userId, cancellationToken))
        {
            AccessToken = await ReadAccessTokenAsync();
            return Page();
        }

        if (!ModelState.IsValid)
        {
            Messages = await _chat.ListMessagesAsync(ConversationId, cancellationToken);
            AccessToken = await ReadAccessTokenAsync();
            return Page();
        }

        var sent = await _chat.SendAsync(ConversationId, userId, "customer", Input.Body.Trim(), cancellationToken);

        try
        {
            var profile = await _accounts.GetProfileAsync(userId, cancellationToken);
            await _email.SendChatNotificationEmailAsync(
                profile?.FullName ?? User.Identity?.Name ?? "Müşteri",
                profile?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? "",
                sent.Body,
                ConversationId.ToString(),
                sent.CreatedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat notification email failed for {Conversation}", ConversationId);
        }

        return RedirectToPage();
    }

    private async Task<bool> TryOpenConversationAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await _chat.GetOrCreateForCustomerAsync(userId, cancellationToken);
            ConversationId = conversation.Id;
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
