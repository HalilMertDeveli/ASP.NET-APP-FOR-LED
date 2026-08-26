using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages;

[Authorize]
public class TaleplerModel : PageModel
{
    private readonly ICustomerRequestStore _requests;
    private readonly IResendEmailService _email;
    private readonly ILogger<TaleplerModel> _logger;

    public TaleplerModel(
        ICustomerRequestStore requests,
        IResendEmailService email,
        ILogger<TaleplerModel> logger)
    {
        _requests = requests;
        _email = email;
        _logger = logger;
    }

    public IReadOnlyList<CustomerRequestRecord> Requests { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public FormStatus Status { get; private set; } = FormStatus.None;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
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
            var created = await _requests.CreateAsync(
                userId,
                Input.Subject.Trim(),
                Input.Description.Trim(),
                Input.Category,
                Input.System,
                string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim(),
                string.IsNullOrWhiteSpace(Input.Company) ? null : Input.Company.Trim(),
                cancellationToken);

            try
            {
                await _email.SendChatNotificationEmailAsync(
                    User.Identity?.Name ?? "Müşteri",
                    User.FindFirstValue(ClaimTypes.Email) ?? "",
                    $"{created.Subject}\n\n{created.Description}",
                    created.ConversationId?.ToString() ?? created.Id.ToString(),
                    created.CreatedAt,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request notification email failed for {Id}", created.Id);
            }

            return RedirectToPage("/Destek", new { requestId = created.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create request failed");
            ErrorMessage = "Talep oluşturulamadı. Lütfen daha sonra tekrar deneyin.";
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

        Requests = await _requests.ListForCustomerAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(Input.Phone))
        {
            Input.Phone = User.FindFirstValue("phone");
        }
    }

    public enum FormStatus { None, Success }

    public sealed class InputModel
    {
        [Required, StringLength(200, MinimumLength = 3)]
        [Display(Name = "Konu")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Kategori")]
        public string Category { get; set; } = "ariza";

        [Display(Name = "Sistem")]
        public string System { get; set; } = "Colorlight";

        [Phone]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [StringLength(160)]
        [Display(Name = "Firma")]
        public string? Company { get; set; }

        [Required, StringLength(4000, MinimumLength = 20)]
        [Display(Name = "Açıklama")]
        public string Description { get; set; } = string.Empty;
    }
}
