namespace LedSupport.Web.Services;

public interface ISupportRequestStore
{
    Task<string> SaveAsync(SupportRequestDto request, CancellationToken cancellationToken = default);

    Task MarkEmailResultAsync(
        string requestId,
        bool emailSent,
        string? emailError,
        CancellationToken cancellationToken = default);
}
