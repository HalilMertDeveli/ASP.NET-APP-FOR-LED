using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LedSupport.Web.Pages;

public class ProductsModel : PageModel
{
    public IActionResult OnGet() => RedirectToPagePermanent("/Services");
}
