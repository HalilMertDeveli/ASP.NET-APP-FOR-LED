using LedSupport.Web.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

/// <summary>
/// Direct path: ASP.NET → Supabase (support_messages) → Resend email.
/// Message is persisted before email; email failure still keeps the DB row.
/// </summary>
public sealed class DirectSupportRequestService : ISupportRequestService
{
    private readonly SupabaseSupportRequestStore _store;
    private readonly IResendEmailService _email;
    private readonly SupportSettings _support;
    private readonly ResendSettings _resend;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DirectSupportRequestService> _logger;

    public DirectSupportRequestService(
        SupabaseSupportRequestStore store,
        IResendEmailService email,
        IOptions<SupportSettings> support,
        IOptions<ResendSettings> resend,
        IMemoryCache cache,
        ILogger<DirectSupportRequestService> logger)
    {
        _store = store;
        _email = email;
        _support = support.Value;
        _resend = resend.Value;
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
                "Set via env Resend__ApiKey or user-secrets.");
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

        string? requestId = null;
        var stored = false;

        try
        {
            requestId = await _store.SaveAsync(request, cancellationToken);
            stored = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Supabase save failed. Configured={Configured}, RequireStore={Require}",
                _store.IsConfigured,
                _support.RequireStore);

            if (_support.RequireStore || _store.IsConfigured)
            {
                // If store is configured (or required), do not pretend success without persistence.
                return SupportSubmitResult.Fail(
                    SupportSubmitResultKind.UpstreamError,
                    "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin veya doğrudan iletişime geçin.");
            }

            requestId = $"mail-{Guid.NewGuid():N}";
            _logger.LogWarning("Continuing without Supabase using id {Id}", requestId);
        }

        try
        {
            await _email.SendSupportRequestEmailAsync(
                request,
                requestId!,
                DateTimeOffset.UtcNow,
                cancellationToken);

            if (stored)
            {
                await _store.MarkEmailResultAsync(requestId!, emailSent: true, emailError: null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend email failed for request {Id}", requestId);

            if (stored)
            {
                await _store.MarkEmailResultAsync(
                    requestId!,
                    emailSent: false,
                    emailError: Truncate(ex.Message, 500),
                    cancellationToken);

                return SupportSubmitResult.Fail(
                    SupportSubmitResultKind.UpstreamError,
                    "Mesajınız kaydedildi ancak e-posta iletilemedi. Lütfen kısa süre sonra tekrar deneyin veya doğrudan iletişime geçin.");
            }

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

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
