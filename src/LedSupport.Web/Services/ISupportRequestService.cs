namespace LedSupport.Web.Services;

public sealed class SupportRequestDto
{
    public required string Name { get; init; }
    public string? Company { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public required string System { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public string? Website { get; init; }
    public string? ClientIp { get; init; }
    public string? UserAgent { get; init; }
    public Guid IdempotencyKey { get; init; }
}

public enum SupportSubmitResultKind
{
    Success,
    RateLimited,
    ConfigurationMissing,
    UpstreamError
}

public sealed class SupportSubmitResult
{
    public SupportSubmitResultKind Kind { get; init; }
    public string? RequestId { get; init; }
    public string? UserMessage { get; init; }

    public static SupportSubmitResult Ok(string? id) => new()
    {
        Kind = SupportSubmitResultKind.Success,
        RequestId = id
    };

    public static SupportSubmitResult Fail(SupportSubmitResultKind kind, string message) => new()
    {
        Kind = kind,
        UserMessage = message
    };
}

public interface ISupportRequestService
{
    Task<SupportSubmitResult> SubmitAsync(SupportRequestDto request, CancellationToken cancellationToken = default);
}
