using Google.Cloud.Firestore;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public interface ISupportRequestStore
{
    Task<string> SaveAsync(SupportRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class FirestoreSupportRequestStore : ISupportRequestStore
{
    private readonly FirebaseSettings _firebase;
    private readonly ILogger<FirestoreSupportRequestStore> _logger;
    private readonly Lazy<FirestoreDb?> _db;

    public FirestoreSupportRequestStore(
        IOptions<FirebaseSettings> firebase,
        ILogger<FirestoreSupportRequestStore> logger)
    {
        _firebase = firebase.Value;
        _logger = logger;
        _db = new Lazy<FirestoreDb?>(CreateDb);
    }

    public async Task<string> SaveAsync(SupportRequestDto request, CancellationToken cancellationToken = default)
    {
        var db = _db.Value
            ?? throw new InvalidOperationException(
                "Firestore is not configured. Set Firebase:CredentialsPath to a service account JSON, " +
                "enable billing, and create the Firestore database.");

        var doc = db.Collection("supportRequests").Document();
        var data = new Dictionary<string, object?>
        {
            ["name"] = request.Name,
            ["company"] = request.Company,
            ["email"] = request.Email,
            ["phone"] = request.Phone,
            ["system"] = request.System,
            ["subject"] = request.Subject,
            ["message"] = request.Message,
            ["clientIp"] = request.ClientIp,
            ["userAgent"] = request.UserAgent,
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["source"] = "aspnet-direct",
            ["status"] = "new",
            ["emailSent"] = false
        };

        await doc.SetAsync(data, cancellationToken: cancellationToken);
        _logger.LogInformation("Support request saved to Firestore {Id}", doc.Id);
        return doc.Id;
    }

    private FirestoreDb? CreateDb()
    {
        if (string.IsNullOrWhiteSpace(_firebase.ProjectId))
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_firebase.CredentialsPath) &&
                !_firebase.CredentialsPath.Contains("YOUR_", StringComparison.Ordinal) &&
                File.Exists(_firebase.CredentialsPath))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _firebase.CredentialsPath);
                return FirestoreDb.Create(_firebase.ProjectId);
            }

            // Application Default Credentials (if configured on the machine)
            return FirestoreDb.Create(_firebase.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firestore client could not be created");
            return null;
        }
    }
}
