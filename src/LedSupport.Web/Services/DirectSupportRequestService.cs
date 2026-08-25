using LedSupport.Web.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

/// <summary>
/// Direct path: ASP.NET → Firestore (Admin) + Resend email.
/// Does not depend on Cloud Functions / Blaze for the happy path.
/// </summary>
public sealed class DirectSupportRequestService : ISupportRequestService
{
    private readonly ISupportRequestStore _store;
    private readonly IResendEmailService _email;
    private readonly SupportSettings _support;
    private readonly ResendSettings _resend;
    private readonly FirebaseSettings _firebase;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DirectSupportRequestService> _logger;

    public DirectSupportRequestService(
        ISupportRequestStore store,
        IResendEmailService email,
        IOptions<SupportSettings> support,
        IOptions<ResendSettings> resend,
        IOptions<FirebaseSettings> firebase,
        IMemoryCache cache,
        ILogger<DirectSupportRequestService> logger)
    {
        _store = store;
        _email = email;
        _support = support.Value;
        _resend = resend.Value;
        _firebase = firebase.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SupportSubmitResult> SubmitAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!IsResendConfigured())
        {
            _logger.LogError(
                "Support submit blocked: Resend:ApiKey missing or placeholder. " +
                "Set via: dotnet user-secrets set \"Resend:ApiKey\" \"re_...\" ");
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.ConfigurationMissing,
                "Talebiniz şu an iletilemedi. Lütfen doğrudan e-posta veya telefon ile ulaşın.");
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp) && IsRateLimited(request.ClientIp))
        {
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.RateLimited,
                "Çok fazla talep gönderildi. Lütfen bir süre sonra tekrar deneyin.");
        }

        string requestId;
        try
        {
            requestId = await _store.SaveAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Firestore save failed. ProjectId={ProjectId}, CredentialsConfigured={HasCreds}, RequireFirestore={Require}",
                _firebase.ProjectId,
                !string.IsNullOrWhiteSpace(_firebase.CredentialsPath) &&
                !_firebase.CredentialsPath.Contains("YOUR_", StringComparison.Ordinal),
                _support.RequireFirestore);

            if (_support.RequireFirestore)
            {
                return SupportSubmitResult.Fail(
                    SupportSubmitResultKind.UpstreamError,
                    "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin veya doğrudan iletişime geçin.");
            }

            requestId = $"local-{Guid.NewGuid():N}";
            _logger.LogWarning("Continuing without Firestore using local id {Id}", requestId);
        }

        try
        {
            await _email.SendSupportRequestEmailAsync(
                request,
                requestId,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend email failed for request {Id}", requestId);
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.UpstreamError,
                "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin veya doğrudan iletişime geçin.");
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp))
        {
            RegisterRateLimitHit(request.ClientIp);
        }

        return SupportSubmitResult.Ok(requestId);
    }

    private bool IsResendConfigured() =>
        !string.IsNullOrWhiteSpace(_resend.ApiKey) &&
        !_resend.ApiKey.Contains("YOUR_", StringComparison.Ordinal);

    private bool IsRateLimited(string clientIp)
    {
        var key = CacheKey(clientIp);
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(Math.Max(1, _support.RateLimitWindowMinutes));
            return 0;
        });
        return count >= Math.Max(1, _support.RateLimitPerWindow);
    }

    private void RegisterRateLimitHit(string clientIp)
    {
        var key = CacheKey(clientIp);
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(Math.Max(1, _support.RateLimitWindowMinutes));
            return 0;
        });
        _cache.Set(
            key,
            count + 1,
            TimeSpan.FromMinutes(Math.Max(1, _support.RateLimitWindowMinutes)));
    }

    private static string CacheKey(string ip) => $"support-rl:{ip}";
}
