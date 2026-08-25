using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace LedSupport.Web.Services;

public sealed class GitHubStatsService : IGitHubStatsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GitHubStatsService> _logger;

    public GitHubStatsService(
        HttpClient http,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<GitHubStatsService> logger)
    {
        _http = http;
        _config = config;
        _cache = cache;
        _logger = logger;

        _http.BaseAddress ??= new Uri("https://api.github.com/");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LedSupport.Web");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var token = _config["GitHub:Token"]
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<GitHubRepoStats> GetRepoStatsAsync(CancellationToken cancellationToken = default)
    {
        var owner = _config["GitHub:Owner"] ?? "HalilMertDeveli";
        var repo = _config["GitHub:Repo"] ?? "ASP.NET-APP-FOR-LED";
        var cacheKey = $"github:{owner}/{repo}";

        if (_cache.TryGetValue(cacheKey, out GitHubRepoStats? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var repoPath = $"repos/{owner}/{Uri.EscapeDataString(repo)}";
            using var repoResponse = await _http.GetAsync(repoPath, cancellationToken);
            if (!repoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub repo request failed: {Status}", repoResponse.StatusCode);
                return Empty();
            }

            await using var repoStream = await repoResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var repoDoc = await JsonDocument.ParseAsync(repoStream, cancellationToken: cancellationToken);
            var root = repoDoc.RootElement;

            var languages = await LoadLanguagesAsync(repoPath, cancellationToken);

            var stats = new GitHubRepoStats
            {
                FullName = root.GetProperty("full_name").GetString() ?? $"{owner}/{repo}",
                Description = root.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? string.Empty
                    : string.Empty,
                HtmlUrl = root.GetProperty("html_url").GetString() ?? string.Empty,
                Stars = root.GetProperty("stargazers_count").GetInt32(),
                Forks = root.GetProperty("forks_count").GetInt32(),
                PrimaryLanguage = root.TryGetProperty("language", out var lang) && lang.ValueKind != JsonValueKind.Null
                    ? lang.GetString()
                    : null,
                UpdatedAt = root.TryGetProperty("updated_at", out var updated)
                    ? updated.GetDateTimeOffset()
                    : null,
                Languages = languages,
                Loaded = true
            };

            _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(15));
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub stats unavailable");
            return Empty();
        }
    }

    private async Task<IReadOnlyList<LanguageShare>> LoadLanguagesAsync(
        string repoPath,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"{repoPath}/languages", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<LanguageShare>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var map = await JsonSerializer.DeserializeAsync<Dictionary<string, long>>(stream, JsonOptions, cancellationToken)
                  ?? new Dictionary<string, long>();

        var total = map.Values.Sum();
        if (total <= 0)
        {
            return Array.Empty<LanguageShare>();
        }

        return map
            .OrderByDescending(x => x.Value)
            .Select(x => new LanguageShare
            {
                Name = x.Key,
                Bytes = x.Value,
                Percent = Math.Round(x.Value * 100.0 / total, 1)
            })
            .ToList();
    }

    private static GitHubRepoStats Empty() => new() { Loaded = false };
}
