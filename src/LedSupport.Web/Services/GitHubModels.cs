namespace LedSupport.Web.Services;

public sealed class GitHubRepoStats
{
    public string FullName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public int Stars { get; init; }
    public int Forks { get; init; }
    public string? PrimaryLanguage { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyList<LanguageShare> Languages { get; init; } = Array.Empty<LanguageShare>();
    public bool Loaded { get; init; }
}

public sealed class LanguageShare
{
    public string Name { get; init; } = string.Empty;
    public long Bytes { get; init; }
    public double Percent { get; init; }
}

public interface IGitHubStatsService
{
    Task<GitHubRepoStats> GetRepoStatsAsync(CancellationToken cancellationToken = default);
}
