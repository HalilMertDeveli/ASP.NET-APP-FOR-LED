using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages;

public class SupportModel : PageModel
{
    public IActionResult OnGet() => RedirectToPagePermanent("/Services");
}
