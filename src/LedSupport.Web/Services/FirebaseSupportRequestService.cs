using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LedSupport.Web.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public sealed class FirebaseSupportRequestService : ISupportRequestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly FirebaseSupportSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FirebaseSupportRequestService> _logger;

    public FirebaseSupportRequestService(
        HttpClient http,
        IOptions<FirebaseSupportSettings> settings,
        IMemoryCache cache,
        ILogger<FirebaseSupportRequestService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SupportSubmitResult> SubmitAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.SubmitUrl) ||
            _settings.SubmitUrl.Contains("YOUR_", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(_settings.IngestSecret) ||
            _settings.IngestSecret.Contains("YOUR_", StringComparison.Ordinal))
        {
            _logger.LogError(
                "Function mode blocked: FirebaseSupport:SubmitUrl configured={HasUrl}, " +
                "FirebaseSupport:IngestSecret configured={HasSecret}. " +
                "Placeholders (YOUR_...) are not accepted. Set user-secrets or switch Support:Mode to Direct.",
                !string.IsNullOrWhiteSpace(_settings.SubmitUrl) &&
                !_settings.SubmitUrl.Contains("YOUR_", StringComparison.Ordinal),
                !string.IsNullOrWhiteSpace(_settings.IngestSecret) &&
                !_settings.IngestSecret.Contains("YOUR_", StringComparison.Ordinal));
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

        using var message = new HttpRequestMessage(HttpMethod.Post, _settings.SubmitUrl);
        message.Headers.TryAddWithoutValidation("X-Support-Secret", _settings.IngestSecret);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = JsonContent.Create(new
        {
            request.Name,
            request.Company,
            request.Email,
            request.Phone,
            request.System,
            request.Subject,
            request.Message,
            request.Website,
            request.ClientIp,
            request.UserAgent
        }, options: JsonOptions);

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return SupportSubmitResult.Fail(
                    SupportSubmitResultKind.RateLimited,
                    "Çok fazla talep gönderildi. Lütfen bir süre sonra tekrar deneyin.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Support function failed: {Status} {Body}",
                    (int)response.StatusCode,
                    payload);
                return SupportSubmitResult.Fail(
                    SupportSubmitResultKind.UpstreamError,
                    "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin veya doğrudan iletişime geçin.");
            }

            string? id = null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("id", out var idEl))
                {
                    id = idEl.GetString();
                }
            }
            catch
            {
                // ignore parse issues; success status is enough for UX
            }

            if (!string.IsNullOrWhiteSpace(request.ClientIp))
            {
                RegisterRateLimitHit(request.ClientIp);
            }

            return SupportSubmitResult.Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Support request submit failed");
            return SupportSubmitResult.Fail(
                SupportSubmitResultKind.UpstreamError,
                "Talebiniz gönderilemedi. Lütfen daha sonra tekrar deneyin veya doğrudan iletişime geçin.");
        }
    }

    private bool IsRateLimited(string clientIp)
    {
        var key = CacheKey(clientIp);
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(Math.Max(1, _settings.RateLimitWindowMinutes));
            return 0;
        });
        return count >= Math.Max(1, _settings.RateLimitPerWindow);
    }

    private void RegisterRateLimitHit(string clientIp)
    {
        var key = CacheKey(clientIp);
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(Math.Max(1, _settings.RateLimitWindowMinutes));
            return 0;
        });
        _cache.Set(
            key,
            count + 1,
            TimeSpan.FromMinutes(Math.Max(1, _settings.RateLimitWindowMinutes)));
    }

    private static string CacheKey(string ip) => $"support-rl:{ip}";
}
