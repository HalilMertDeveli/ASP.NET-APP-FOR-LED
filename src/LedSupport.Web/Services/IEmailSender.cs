namespace LedSupport.Web.Services;

public sealed class SupportRequestEmail
{
    public required string Name { get; init; }
    public string? Company { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public required string System { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
}

public interface IEmailSender
{
    Task SendSupportRequestAsync(SupportRequestEmail request, CancellationToken cancellationToken = default);
}
