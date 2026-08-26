using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LedSupport.Web.Options;
using Microsoft.Extensions.Options;

namespace LedSupport.Web.Services;

public sealed record ConversationRecord(
    Guid Id,
    string CustomerId,
    Guid? RequestId,
    string Status,
    string? LastMessage,
    DateTimeOffset? LastMessageAt);

public sealed record ConversationListItem(
    Guid Id,
    string CustomerId,
    Guid? RequestId,
    string CustomerName,
    string CustomerEmail,
    string? Subject,
    string Status,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);

public sealed record ChatMessage(
    Guid Id,
    Guid ConversationId,
    string? SenderId,
    string SenderRole,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public interface IChatStore
{
    Task<ConversationRecord> GetOrCreateForCustomerAsync(string customerId, CancellationToken cancellationToken = default);
    Task<ConversationRecord?> GetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<ConversationRecord?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<ChatMessage?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> ListMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid conversationId, string viewerRole, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid conversationId, string viewerRole, CancellationToken cancellationToken = default);
    Task MarkMessageReadAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<ChatMessage> SendAsync(Guid conversationId, string senderId, string senderRole, string body, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationListItem>> ListForAdminAsync(CancellationToken cancellationToken = default);
}

public sealed class SupabaseChatStore : IChatStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;
    private readonly ILogger<SupabaseChatStore> _logger;

    public SupabaseChatStore(
        HttpClient http,
        IOptions<SupabaseSettings> settings,
        ILogger<SupabaseChatStore> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ConversationRecord> GetOrCreateForCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByCustomerAsync(customerId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        EnsureConfigured();
        var payload = new { customer_id = customerId, status = "open" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/conversations")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if ((int)response.StatusCode == 409)
        {
            return await FindByCustomerAsync(customerId, cancellationToken)
                   ?? throw new InvalidOperationException("Conversation already exists but could not be loaded.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Conversation insert failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new InvalidOperationException("Destek konuşması oluşturulamadı.");
        }

        return ParseOne<ConversationRow>(body)?.ToRecord()
               ?? throw new InvalidOperationException("Destek konuşması oluşturulamadı.");
    }

    public async Task<ConversationRecord?> GetByRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/conversations?request_id=eq.{requestId:D}&select=id,customer_id,request_id,status,last_message,last_message_at&limit=1");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseOne<ConversationRow>(body)?.ToRecord();
    }

    public async Task<ConversationRecord?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/conversations?id=eq.{conversationId:D}&select=id,customer_id,request_id,status,last_message,last_message_at&limit=1");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseOne<ConversationRow>(body)?.ToRecord();
    }

    public async Task<ChatMessage?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/messages?id=eq.{messageId:D}&select=id,conversation_id,sender_id,sender_role,body,created_at,read_at&limit=1");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseOne<MessageRow>(body)?.ToRecord();
    }

    public async Task<IReadOnlyList<ChatMessage>> ListMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/messages?conversation_id=eq.{conversationId:D}&select=id,conversation_id,sender_id,sender_role,body,created_at,read_at&order=created_at.asc");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("List messages failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            return [];
        }

        return ParseMany<MessageRow>(body).Select(x => x.ToRecord()).ToList();
    }

    public async Task<int> CountUnreadAsync(
        Guid conversationId,
        string viewerRole,
        CancellationToken cancellationToken = default)
    {
        var other = string.Equals(viewerRole, "admin", StringComparison.OrdinalIgnoreCase) ? "customer" : "admin";
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/messages?conversation_id=eq.{conversationId:D}&sender_role=eq.{other}&read_at=is.null&select=id");
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Content-Range", out var ranges))
        {
            var range = ranges.FirstOrDefault() ?? "";
            var slash = range.LastIndexOf('/');
            if (slash >= 0 && int.TryParse(range[(slash + 1)..], out var total))
            {
                return total;
            }
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMany<MessageRow>(body).Count;
    }

    public async Task MarkReadAsync(Guid conversationId, string viewerRole, CancellationToken cancellationToken = default)
    {
        var other = string.Equals(viewerRole, "admin", StringComparison.OrdinalIgnoreCase) ? "customer" : "admin";
        EnsureConfigured();
        var payload = new { read_at = DateTimeOffset.UtcNow };
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/messages?conversation_id=eq.{conversationId:D}&sender_role=eq.{other}&read_at=is.null")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        await _http.SendAsync(request, cancellationToken);
    }

    public async Task MarkMessageReadAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var payload = new { read_at = DateTimeOffset.UtcNow };
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"rest/v1/messages?id=eq.{messageId:D}&read_at=is.null")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        await _http.SendAsync(request, cancellationToken);
    }

    public async Task<ChatMessage> SendAsync(
        Guid conversationId,
        string senderId,
        string senderRole,
        string body,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var trimmed = body.Trim();
        var recent = await ListMessagesAsync(conversationId, cancellationToken);
        var last = recent.LastOrDefault();
        if (last is not null &&
            string.Equals(last.SenderId, senderId, StringComparison.Ordinal) &&
            string.Equals(last.Body, trimmed, StringComparison.Ordinal) &&
            DateTimeOffset.UtcNow - last.CreatedAt < TimeSpan.FromSeconds(12))
        {
            return last;
        }

        var payload = new
        {
            conversation_id = conversationId,
            sender_id = senderId,
            sender_role = senderRole,
            body = trimmed,
            message_type = "text"
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/messages")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyServiceRole(request);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Send message failed: {Status} {Body}", (int)response.StatusCode, Truncate(json, 400));
            throw new InvalidOperationException("Mesaj gönderilemedi.");
        }

        return ParseOne<MessageRow>(json)?.ToRecord()
               ?? throw new InvalidOperationException("Mesaj gönderilemedi.");
    }

    public async Task<IReadOnlyList<ConversationListItem>> ListForAdminAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/conversations?select=id,customer_id,request_id,status,last_message,last_message_at,updated_at,profiles!conversations_customer_id_fkey(full_name,email),customer_requests!conversations_request_id_fkey(subject)&order=updated_at.desc");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Admin conversation list failed: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            return [];
        }

        var rows = ParseMany<AdminConversationRow>(body);
        var unread = await LoadUnreadByConversationAsync(cancellationToken);
        return rows.Select(row =>
        {
            var id = row.Id;
            unread.TryGetValue(id, out var count);
            return new ConversationListItem(
                id,
                row.CustomerId,
                row.RequestId,
                string.IsNullOrWhiteSpace(row.Profiles?.FullName) ? "Müşteri" : row.Profiles.FullName,
                row.Profiles?.Email ?? "",
                row.CustomerRequests?.Subject,
                row.Status,
                row.LastMessage,
                row.LastMessageAt,
                count);
        }).ToList();
    }

    private async Task<Dictionary<Guid, int>> LoadUnreadByConversationAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "rest/v1/messages?sender_role=eq.customer&read_at=is.null&select=conversation_id");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMany<UnreadRow>(body)
            .GroupBy(x => x.ConversationId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<ConversationRecord?> FindByCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"rest/v1/conversations?customer_id=eq.{Uri.EscapeDataString(customerId)}&select=id,customer_id,request_id,status,last_message,last_message_at&order=updated_at.desc&limit=1");
        ApplyServiceRole(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseOne<ConversationRow>(body)?.ToRecord();
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

    private sealed class ConversationRow
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = "";
        public Guid? RequestId { get; set; }
        public string Status { get; set; } = "open";
        public string? LastMessage { get; set; }
        public DateTimeOffset? LastMessageAt { get; set; }

        public ConversationRecord ToRecord() => new(Id, CustomerId, RequestId, Status, LastMessage, LastMessageAt);
    }

    private sealed class MessageRow
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public string? SenderId { get; set; }
        public string SenderRole { get; set; } = "customer";
        public string Body { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }

        public ChatMessage ToRecord() => new(Id, ConversationId, SenderId, SenderRole, Body, CreatedAt, ReadAt);
    }

    private sealed class AdminConversationRow
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = "";
        public Guid? RequestId { get; set; }
        public string Status { get; set; } = "open";
        public string? LastMessage { get; set; }
        public DateTimeOffset? LastMessageAt { get; set; }
        public ProfileEmbed? Profiles { get; set; }
        public RequestEmbed? CustomerRequests { get; set; }
    }

    private sealed class RequestEmbed
    {
        public string? Subject { get; set; }
    }

    private sealed class ProfileEmbed
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    private sealed class UnreadRow
    {
        public Guid ConversationId { get; set; }
    }
}
