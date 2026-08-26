using System.Collections.Concurrent;
using LedSupport.Web.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

/// <summary>
/// Persist to Supabase first, then send Resend. Duplicate posts reuse the row and skip a second mail if already sent.
/// </summary>
public sealed class DirectSupportRequestService : ISupportRequestService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SendLocks = new();

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
        if (!string.IsNullOrWhiteSpace(request.ClientIp) && IsRateLimited(request.ClientIp))
        {
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.RateLimited,
                "Çok fazla talep gönderildi. Lütfen bir süre sonra tekrar deneyin.");
        }

        var sendLock = request.IdempotencyKey == Guid.Empty
            ? new SemaphoreSlim(1, 1)
            : SendLocks.GetOrAdd(request.IdempotencyKey, _ => new SemaphoreSlim(1, 1));

        await sendLock.WaitAsync(cancellationToken);
        try
        {
            return await SubmitCoreAsync(request, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private async Task<SupportSubmitResult> SubmitCoreAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken)
    {
        SupportMessageSaveResult saved;
        try
        {
            saved = await _store.SaveAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Supabase save failed. Configured={Configured}, RequireStore={Require}",
                _store.IsConfigured,
                _support.RequireStore);

            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.UpstreamError,
                "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin veya doğrudan iletişime geçin.");
        }

        if (saved.EmailAlreadySent)
        {
            if (!string.IsNullOrWhiteSpace(request.ClientIp))
            {
                RegisterRateLimitHit(request.ClientIp);
            }

            return SupportSubmitResult.Ok(saved.Id);
        }

        if (!IsResendConfigured())
        {
            await _store.MarkEmailResultAsync(
                saved.Id,
                emailSent: false,
                emailError: "Resend:ApiKey missing",
                cancellationToken);

            _logger.LogError("Support email skipped: Resend:ApiKey missing after persisting {Id}", saved.Id);
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.ConfigurationMissing,
                "Talebiniz kaydedildi ancak e-posta şu an iletilemedi. Lütfen doğrudan e-posta veya telefon ile ulaşın.");
        }

        var claimed = await _store.TryClaimEmailSendAsync(saved.Id, cancellationToken);
        if (claimed == EmailSendClaimResult.AlreadyHandled)
        {
            _logger.LogInformation("Skipped duplicate mail for already claimed/sent request {Id}", saved.Id);
            if (!string.IsNullOrWhiteSpace(request.ClientIp))
            {
                RegisterRateLimitHit(request.ClientIp);
            }

            return SupportSubmitResult.Ok(saved.Id);
        }

        if (claimed != EmailSendClaimResult.Claimed)
        {
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.UpstreamError,
                "Mesajınız kaydedildi ancak e-posta iletilemedi. Lütfen kısa süre sonra tekrar deneyin veya doğrudan iletişime geçin.");
        }

        try
        {
            await _email.SendSupportRequestEmailAsync(
                request,
                saved.Id,
                DateTimeOffset.UtcNow,
                cancellationToken);

            await _store.MarkEmailResultAsync(saved.Id, emailSent: true, emailError: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend email failed for request {Id}", saved.Id);

            await _store.MarkEmailResultAsync(
                saved.Id,
                emailSent: false,
                emailError: Truncate(ex.Message, 500),
                cancellationToken);

            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.UpstreamError,
                "Mesajınız kaydedildi ancak e-posta iletilemedi. Lütfen kısa süre sonra tekrar deneyin veya doğrudan iletişime geçin.");
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp))
        {
            RegisterRateLimitHit(request.ClientIp);
        }

        return SupportSubmitResult.Ok(saved.Id);
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
