using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public sealed record CustomerRequestRecord(
    Guid Id,
    string CustomerId,
    string Subject,
    string Description,
    string Category,
    string? System,
    string? Phone,
    string? Company,
    string Status,
    string Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? ConversationId);

public sealed record CustomerRequestListItem(
    Guid Id,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    string Subject,
    string Category,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    int UnreadCount,
    Guid? ConversationId);

public sealed record CustomerListItem(
    string Id,
    string FullName,
    string Email,
    string? Phone,
    string? Company,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    int RequestCount,
    DateTimeOffset? LastMessageAt);

public sealed record AdminDashboardStats(
    int TotalCustomers,
    int NewCustomers7d,
    int OpenRequests,
    int InProgressRequests,
    int WaitingCustomerRequests,
    int ResolvedRequests,
    int ClosedRequests,
    int UnreadMessages);

public interface ICustomerRequestStore
{
    Task<CustomerRequestRecord> CreateAsync(
        string customerId,
        string subject,
        string description,
        string category,
        string? system,
        string? phone,
        string? company,
        CancellationToken cancellationToken = default);

    Task<CustomerRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerRequestRecord>> ListForCustomerAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerRequestListItem>> ListForAdminAsync(string? status, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<AdminDashboardStats> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerListItem>> ListCustomersAsync(CancellationToken cancellationToken = default);
}

public sealed class CustomerRequestStore : ICustomerRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;
    private readonly ILogger<CustomerRequestStore> _logger;

    public CustomerRequestStore(
        HttpClient http,
        IOptions<SupabaseSettings> settings,
        ILogger<CustomerRequestStore> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CustomerRequestRecord> CreateAsync(
        string customerId,
        string subject,
        string description,
        string category,
        string? system,
        string? phone,
        string? company,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var payload = new
        {
            customer_id = customerId,
            subject,
            description,
            category,
            system,
            phone,
            company,
            status = "open",
            priority = "normal"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/customer_requests")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Request insert failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new InvalidOperationException("Talep oluşturulamadı.");
        }

        var row = ParseOne<RequestRow>(body) ?? throw new InvalidOperationException("Talep oluşturulamadı.");
        var conversation = await FindConversationByRequestAsync(row.Id, cancellationToken);
        return row.ToRecord(conversation?.Id);
    }

    public async Task<CustomerRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/customer_requests?id=eq.{id:D}&select=id,customer_id,subject,description,category,system,phone,company,status,priority,created_at,updated_at&limit=1");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var row = ParseOne<RequestRow>(body);
        if (row is null)
        {
            return null;
        }

        var conversation = await FindConversationByRequestAsync(row.Id, cancellationToken);
        return row.ToRecord(conversation?.Id);
    }

    public async Task<IReadOnlyList<CustomerRequestRecord>> ListForCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/customer_requests?customer_id=eq.{Uri.EscapeDataString(customerId)}&select=id,customer_id,subject,description,category,system,phone,company,status,priority,created_at,updated_at&order=created_at.desc");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var rows = ParseMany<RequestRow>(body);
        var result = new List<CustomerRequestRecord>(rows.Count);
        foreach (var row in rows)
        {
            var conversation = await FindConversationByRequestAsync(row.Id, cancellationToken);
            result.Add(row.ToRecord(conversation?.Id));
        }

        return result;
    }

    public async Task<IReadOnlyList<CustomerRequestListItem>> ListForAdminAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var filter = string.IsNullOrWhiteSpace(status) ? "" : $"status=eq.{Uri.EscapeDataString(status)}&";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/customer_requests?{filter}select=id,customer_id,subject,category,status,created_at,profiles!customer_requests_customer_id_fkey(full_name,email)&order=created_at.desc");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Admin request list failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            return [];
        }

        var rows = ParseMany<AdminRequestRow>(body);
        var unread = await LoadUnreadByRequestAsync(cancellationToken);
        var last = await LoadLastMessageByRequestAsync(cancellationToken);
        var conversations = await LoadConversationsByRequestAsync(cancellationToken);
        return rows.Select(row =>
        {
            unread.TryGetValue(row.Id, out var count);
            last.TryGetValue(row.Id, out var lastAt);
            conversations.TryGetValue(row.Id, out var conversationId);
            return new CustomerRequestListItem(
                row.Id,
                row.CustomerId,
                string.IsNullOrWhiteSpace(row.Profiles?.FullName) ? "Müşteri" : row.Profiles.FullName,
                row.Profiles?.Email ?? "",
                row.Subject,
                row.Category,
                row.Status,
                row.CreatedAt,
                lastAt,
                count,
                conversationId == Guid.Empty ? null : conversationId);
        }).ToList();
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var payload = new { status };
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/customer_requests?id=eq.{id:D}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Status update failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new InvalidOperationException("Talep durumu güncellenemedi.");
        }
    }

    public async Task<AdminDashboardStats> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var customers = await CountAsync("profiles?role=eq.customer", cancellationToken);
        var since = DateTimeOffset.UtcNow.AddDays(-7).ToString("o");
        var newCustomers = await CountAsync(
            $"profiles?role=eq.customer&created_at=gte.{Uri.EscapeDataString(since)}",
            cancellationToken);
        var open = await CountAsync("customer_requests?status=eq.open", cancellationToken);
        var progress = await CountAsync("customer_requests?status=eq.in_progress", cancellationToken);
        var waiting = await CountAsync("customer_requests?status=eq.waiting_customer", cancellationToken);
        var resolved = await CountAsync("customer_requests?status=eq.resolved", cancellationToken);
        var closed = await CountAsync("customer_requests?status=eq.closed", cancellationToken);
        var unread = await CountAsync("messages?sender_role=eq.customer&read_at=is.null", cancellationToken);
        return new AdminDashboardStats(customers, newCustomers, open, progress, waiting, resolved, closed, unread);
    }

    public async Task<IReadOnlyList<CustomerListItem>> ListCustomersAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/profiles?role=eq.customer&select=id,full_name,email,phone,company,avatar_url,created_at,last_login_at&order=created_at.desc");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var profiles = ParseMany<ProfileListRow>(body);
        var requestCounts = await LoadRequestCountsAsync(cancellationToken);
        var lastMessages = await LoadLastMessageByCustomerAsync(cancellationToken);
        return profiles.Select(p =>
        {
            requestCounts.TryGetValue(p.Id, out var count);
            lastMessages.TryGetValue(p.Id, out var lastAt);
            return new CustomerListItem(
                p.Id,
                string.IsNullOrWhiteSpace(p.FullName) ? p.Email : p.FullName,
                p.Email,
                p.Phone,
                p.Company,
                p.AvatarUrl,
                p.CreatedAt,
                p.LastLoginAt,
                count,
                lastAt);
        }).ToList();
    }

    private async Task<Dictionary<Guid, int>> LoadUnreadByRequestAsync(CancellationToken cancellationToken)
    {
        using var convRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/conversations?select=id,request_id");
        ApplyServiceRole(convRequest);
        using var convResponse = await _http.SendAsync(convRequest, cancellationToken);
        if (!convResponse.IsSuccessStatusCode)
        {
            return [];
        }

        var convBody = await convResponse.Content.ReadAsStringAsync(cancellationToken);
        var conversations = ParseMany<ConversationMapRow>(convBody)
            .Where(x => x.RequestId is not null)
            .ToDictionary(x => x.Id, x => x.RequestId!.Value);

        using var msgRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/messages?sender_role=eq.customer&read_at=is.null&select=conversation_id");
        ApplyServiceRole(msgRequest);
        using var msgResponse = await _http.SendAsync(msgRequest, cancellationToken);
        if (!msgResponse.IsSuccessStatusCode)
        {
            return [];
        }

        var msgBody = await msgResponse.Content.ReadAsStringAsync(cancellationToken);
        var counts = new Dictionary<Guid, int>();
        foreach (var row in ParseMany<UnreadRow>(msgBody))
        {
            if (!conversations.TryGetValue(row.ConversationId, out var requestId))
            {
                continue;
            }

            counts[requestId] = counts.GetValueOrDefault(requestId) + 1;
        }

        return counts;
    }

    private async Task<Dictionary<Guid, Guid>> LoadConversationsByRequestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/conversations?select=id,request_id");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMany<ConversationMapRow>(body)
            .Where(x => x.RequestId is not null)
            .GroupBy(x => x.RequestId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);
    }

    private async Task<Dictionary<Guid, DateTimeOffset>> LoadLastMessageByRequestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/conversations?select=request_id,last_message_at");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMany<ConversationMapRow>(body)
            .Where(x => x.RequestId is not null && x.LastMessageAt is not null)
            .GroupBy(x => x.RequestId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(x => x.LastMessageAt)!.Value);
    }

    private async Task<Dictionary<string, int>> LoadRequestCountsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/customer_requests?select=customer_id");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMany<CustomerIdRow>(body)
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<Dictionary<string, DateTimeOffset>> LoadLastMessageByCustomerAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/conversations?select=customer_id,last_message_at");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMany<CustomerLastRow>(body)
            .Where(x => x.LastMessageAt is not null)
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.LastMessageAt)!.Value);
    }

    private async Task<int> CountAsync(string path, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"rest/v1/{path}&select=id");
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        request.Headers.TryAddWithoutValidation("Range", "0-0");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Content-Range", out var ranges))
        {
            var range = ranges.FirstOrDefault() ?? "";
            var slash = range.LastIndexOf('/');
            if (slash >= 0 && int.TryParse(range[(slash + 1)..], out var total) && total >= 0)
            {
                return total;
            }
        }

        return 0;
    }

    private async Task<ConversationMapRow?> FindConversationByRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/conversations?request_id=eq.{requestId:D}&select=id,request_id,last_message_at&limit=1");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseOne<ConversationMapRow>(body);
    }

    private void EnsureConfigured()
    {
        if (!_settings.HasServiceRole)
        {
            throw new InvalidOperationException("Supabase is not configured.");
        }

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(_settings.Url.TrimEnd('/') + "/");
        }
    }

    private void ApplyServiceRole(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("apikey", _settings.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
    }

    private static T? ParseOne<T>(string json) => ParseMany<T>(json).FirstOrDefault();

    private static List<T> ParseMany<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private sealed class RequestRow
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "genel";
        public string? System { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string Status { get; set; } = "open";
        public string Priority { get; set; } = "normal";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public CustomerRequestRecord ToRecord(Guid? conversationId) => new(
            Id, CustomerId, Subject, Description, Category, System, Phone, Company, Status, Priority, CreatedAt, UpdatedAt, conversationId);
    }

    private sealed class AdminRequestRow
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Category { get; set; } = "";
        public string Status { get; set; } = "open";
        public DateTimeOffset CreatedAt { get; set; }
        public ProfileEmbed? Profiles { get; set; }
    }

    private sealed class ProfileEmbed
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    private sealed class ProfileListRow
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
    }

    private sealed class ConversationMapRow
    {
        public Guid Id { get; set; }
        public Guid? RequestId { get; set; }
        public DateTimeOffset? LastMessageAt { get; set; }
    }

    private sealed class UnreadRow
    {
        public Guid ConversationId { get; set; }
    }

    private sealed class CustomerIdRow
    {
        public string CustomerId { get; set; } = "";
    }

    private sealed class CustomerLastRow
    {
        public string CustomerId { get; set; } = "";
        public DateTimeOffset? LastMessageAt { get; set; }
    }
}
