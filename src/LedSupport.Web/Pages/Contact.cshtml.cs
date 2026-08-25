using System.ComponentModel.DataAnnotations;
using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Pages;

public class ContactModel : PageModel
{
    private readonly ISupportRequestService _supportRequests;
    private readonly SiteSettings _site;
    private readonly ILogger<ContactModel> _logger;

    public ContactModel(
        ISupportRequestService supportRequests,
        IOptions<SiteSettings> site,
        ILogger<ContactModel> logger)
    {
        _supportRequests = supportRequests;
        _site = site.Value;
        _logger = logger;
    }

    [BindProperty]
    public ContactInput Input { get; set; } = new();

    public FormStatus Status { get; private set; } = FormStatus.None;
    public string? ErrorMessage { get; private set; }
    public string? SuccessRequestId { get; private set; }

    public string SiteEmail => _site.Email;
    public string SitePhone => _site.Phone;
    public string SitePhoneHref => _site.PhoneHref;
    public string SiteWhatsApp => _site.WhatsAppUrl;

    public List<SelectListItem> SystemOptions { get; } =
    [
        new("Colorlight", "Colorlight"),
        new("NovaStar", "NovaStar"),
        new("Huidu", "Huidu"),
        new("Diğer", "Diğer")
    ];

    public void OnGet([FromQuery] string? system)
    {
        if (!string.IsNullOrWhiteSpace(system) &&
            SystemOptions.Any(x => string.Equals(x.Value, system, StringComparison.OrdinalIgnoreCase)))
        {
            Input.System = system;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Input.Website))
        {
            _logger.LogWarning("Contact honeypot triggered");
            Status = FormStatus.Success;
            ModelState.Clear();
            Input = new ContactInput();
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _supportRequests.SubmitAsync(new SupportRequestDto
        {
            Name = Input.Name.Trim(),
            Company = string.IsNullOrWhiteSpace(Input.Company) ? null : Input.Company.Trim(),
            Email = Input.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim(),
            System = Input.System,
            Subject = Input.Subject.Trim(),
            Message = Input.Message.Trim(),
            Website = Input.Website,
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, cancellationToken);

        if (result.Kind == SupportSubmitResultKind.Success)
        {
            Status = FormStatus.Success;
            SuccessRequestId = result.RequestId;
            ModelState.Clear();
            Input = new ContactInput();
            return Page();
        }

        Status = FormStatus.Failed;
        ErrorMessage = result.UserMessage
            ?? "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin.";
        return Page();
    }

    public enum FormStatus
    {
        None,
        Success,
        Failed
    }

    public sealed class ContactInput
    {
        [Required(ErrorMessage = "Ad soyad gerekli")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Ad soyad en az 2 karakter olmalı")]
        [Display(Name = "Ad Soyad")]
        public string Name { get; set; } = string.Empty;

        [StringLength(160)]
        [Display(Name = "Firma adı (opsiyonel)")]
        public string? Company { get; set; }

        [Required(ErrorMessage = "E-posta gerekli")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;

        [StringLength(40)]
        [Display(Name = "Telefon (opsiyonel)")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Sistem seçimi gerekli")]
        [Display(Name = "Kullanılan sistem")]
        public string System { get; set; } = "Colorlight";

        [Required(ErrorMessage = "Konu gerekli")]
        [StringLength(200, MinimumLength = 3)]
        [Display(Name = "Konu")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sorun açıklaması gerekli")]
        [StringLength(4000, MinimumLength = 20, ErrorMessage = "Açıklama en az 20 karakter olmalı")]
        [Display(Name = "Sorun açıklaması")]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "Website")]
        public string? Website { get; set; }
    }
}
