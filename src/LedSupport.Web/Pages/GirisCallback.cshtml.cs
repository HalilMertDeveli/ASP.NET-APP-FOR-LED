using LedSupport.Web.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Pages;

[AllowAnonymous]
public class GirisCallbackModel : PageModel
{
    private readonly SupabaseSettings _supabase;

    public GirisCallbackModel(IOptions<SupabaseSettings> supabase)
    {
        _supabase = supabase.Value;
    }

    public string SupabaseUrl => _supabase.Url?.TrimEnd('/') ?? "";
    public string PublishableKey => _supabase.PublishableKey;
}
