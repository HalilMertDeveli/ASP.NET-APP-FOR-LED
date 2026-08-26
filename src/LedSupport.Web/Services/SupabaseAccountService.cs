using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public sealed record AppUser(
    string Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    string Role,
    DateTimeOffset CreatedAt,
    string? Phone = null,
    string? Company = null,
    DateTimeOffset? LastLoginAt = null);

public interface ISupabaseAccountService
{
    Task<AppUser?> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<AppUser?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateContactAsync(string userId, string? phone, string? company, CancellationToken cancellationToken = default);
    Task TouchLastLoginAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class SupabaseAccountService : ISupabaseAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;
    private readonly ILogger<SupabaseAccountService> _logger;

    public SupabaseAccountService(
        HttpClient http,
        IOptions<SupabaseSettings> settings,
        ILogger<SupabaseAccountService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AppUser?> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || !_settings.HasPublicClient)
        {
            return null;
        }

        EnsureBaseAddress();

        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/v1/user");
        request.Headers.TryAddWithoutValidation("apikey", _settings.PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Supabase auth/user failed: {Status}", (int)response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string? fullName = null;
        string? avatar = null;
        if (root.TryGetProperty("user_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            fullName = ReadMeta(meta, "full_name") ?? ReadMeta(meta, "name");
            avatar = ReadMeta(meta, "avatar_url") ?? ReadMeta(meta, "picture");
        }

        var profile = await WaitForProfileAsync(id, cancellationToken)
                      ?? await CreateProfileAsync(id, email, fullName, avatar, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(fullName) || !string.IsNullOrWhiteSpace(avatar))
        {
            await PatchProfileAsync(id, fullName, avatar, cancellationToken);
            profile = await GetProfileAsync(id, cancellationToken) ?? profile;
        }

        await TouchLastLoginAsync(id, cancellationToken);
        return profile;
    }

    public async Task<AppUser?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !_settings.HasServiceRole)
        {
            return null;
        }

        EnsureBaseAddress();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId)}&select=id,email,full_name,avatar_url,role,created_at&limit=1");
        ApplyServiceRole(request);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var rows = JsonSerializer.Deserialize<List<ProfileRow>>(body, JsonOptions);
        var row = rows?.FirstOrDefault();
        return row is null ? null : ToUser(row);
    }

    public async Task UpdateContactAsync(
        string userId,
        string? phone,
        string? company,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !_settings.HasServiceRole)
        {
            throw new InvalidOperationException("Supabase is not configured.");
        }

        EnsureBaseAddress();
        var payload = new Dictionary<string, object?>
        {
            ["phone"] = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            ["company"] = string.IsNullOrWhiteSpace(company) ? null : company.Trim()
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId)}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Profile contact update failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new InvalidOperationException("Profil güncellenemedi.");
        }
    }

    public async Task TouchLastLoginAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !_settings.HasServiceRole)
        {
            return;
        }

        EnsureBaseAddress();
        var payload = new { last_login_at = DateTimeOffset.UtcNow };
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId)}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        await _http.SendAsync(request, cancellationToken);
    }

    public async Task DeleteAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !_settings.HasServiceRole)
        {
            throw new InvalidOperationException("Supabase is not configured.");
        }

        EnsureBaseAddress();

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"auth/v1/admin/users/{Uri.EscapeDataString(userId)}");
        ApplyServiceRole(request);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Delete user failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new InvalidOperationException("Hesap silinemedi.");
        }
    }

    private async Task<AppUser?> WaitForProfileAsync(string userId, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 5; i++)
        {
            var profile = await GetProfileAsync(userId, cancellationToken);
            if (profile is not null)
            {
                return profile;
            }

            await Task.Delay(250, cancellationToken);
        }

        return null;
    }

    private async Task<AppUser?> CreateProfileAsync(
        string userId,
        string email,
        string? fullName,
        string? avatar,
        CancellationToken cancellationToken)
    {
        EnsureBaseAddress();

        var payload = new
        {
            id = userId,
            email,
            full_name = string.IsNullOrWhiteSpace(fullName) ? email.Split('@')[0] : fullName,
            avatar_url = avatar,
            role = "customer"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/profiles")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation,resolution=merge-duplicates");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Profile insert failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            return await GetProfileAsync(userId, cancellationToken);
        }

        return await GetProfileAsync(userId, cancellationToken);
    }

    private async Task PatchProfileAsync(
        string userId,
        string? fullName,
        string? avatar,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            payload["full_name"] = fullName;
        }

        if (!string.IsNullOrWhiteSpace(avatar))
        {
            payload["avatar_url"] = avatar;
        }

        if (payload.Count == 0)
        {
            return;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId)}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        await _http.SendAsync(request, cancellationToken);
    }

    private void EnsureBaseAddress()
    {
        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_settings.Url))
        {
            _http.BaseAddress = new Uri(_settings.Url.TrimEnd('/') + "/");
        }
    }

    private void ApplyServiceRole(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
    }

    private static AppUser ToUser(ProfileRow row) => new(
        row.Id,
        row.Email,
        string.IsNullOrWhiteSpace(row.FullName) ? row.Email : row.FullName,
        row.AvatarUrl,
        string.Equals(row.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "customer",
        row.CreatedAt,
        row.Phone,
        row.Company,
        row.LastLoginAt);

    private static string? ReadMeta(JsonElement meta, string name) =>
        meta.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private sealed class ProfileRow
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = "customer";
        public DateTimeOffset CreatedAt { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
    }
}
