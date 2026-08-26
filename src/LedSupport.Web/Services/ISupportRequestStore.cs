namespace LedSupport.Web.Services;

public sealed record SupportMessageSaveResult
{
    public required string Id { get; init; }
    public required string EmailStatus { get; init; }
    public bool AlreadyPersisted { get; init; }

    public bool EmailAlreadySent =>
        string.Equals(EmailStatus, "sent", StringComparison.OrdinalIgnoreCase);
}

public interface ISupportRequestStore
{
    Task<SupportMessageSaveResult> SaveAsync(
        SupportRequestDto request,
        CancellationToken cancellationToken = default);

    Task MarkEmailResultAsync(
        string requestId,
        bool emailSent,
        string? emailError,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a row for sending.
    /// </summary>
    Task<EmailSendClaimResult> TryClaimEmailSendAsync(
        string requestId,
        CancellationToken cancellationToken = default);
}

public enum EmailSendClaimResult
{
    Claimed,
    AlreadyHandled,
    Failed
}
