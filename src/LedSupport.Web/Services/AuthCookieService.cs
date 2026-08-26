using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LedSupport.Web.Services;

public static class AuthCookieService
{
    public const string AccessTokenClaim = "supabase_access_token";
    public const string RefreshTokenClaim = "supabase_refresh_token";
    public const string AvatarClaim = "avatar_url";

    public static async Task SignInAsync(
        HttpContext http,
        AppUser user,
        string accessToken,
        string refreshToken)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AccessTokenClaim, accessToken)
        };

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim(AvatarClaim, user.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }

    public static string? GetAccessToken(AuthenticateResult result)
    {
        return result.Principal?.FindFirstValue(AccessTokenClaim);
    }
}
