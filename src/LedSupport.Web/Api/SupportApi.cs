using System.Security.Claims;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedSupport.Web.Api;

public static class SupportApi
{
    public static void MapSupportApi(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/profile", GetProfileAsync);
        group.MapGet("/requests", ListMyRequestsAsync);
        group.MapPost("/requests", CreateRequestAsync);
        group.MapGet("/requests/{id:guid}", GetRequestAsync);
        group.MapGet("/conversations/{id:guid}", GetConversationAsync);
        group.MapGet("/conversations/{id:guid}/messages", ListMessagesAsync);
        group.MapPost("/conversations/{id:guid}/messages", SendMessageAsync);
        group.MapPost("/messages/{id:guid}/read", MarkReadAsync);

        var admin = group.MapGroup("/admin").RequireAuthorization("AdminOnly");
        admin.MapGet("/customers", ListCustomersAsync);
        admin.MapGet("/customers/{id}", GetCustomerAsync);
        admin.MapGet("/requests", ListAdminRequestsAsync);
        admin.MapGet("/conversations", ListAdminConversationsAsync);
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal user,
        ISupabaseAccountService accounts,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var profile = await accounts.GetProfileAsync(userId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Json(profile);
    }

    private static async Task<IResult> ListMyRequestsAsync(
        ClaimsPrincipal user,
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        return Results.Json(await requests.ListForCustomerAsync(userId, cancellationToken));
    }

    private static async Task<IResult> CreateRequestAsync(
        CreateRequestBody body,
        ClaimsPrincipal user,
        ICustomerRequestStore requests,
        IResendEmailService email,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.Subject) || string.IsNullOrWhiteSpace(body.Description))
        {
            return Results.BadRequest(new { error = "Konu ve açıklama gerekli." });
        }

        var created = await requests.CreateAsync(
            userId,
            body.Subject.Trim(),
            body.Description.Trim(),
            string.IsNullOrWhiteSpace(body.Category) ? "genel" : body.Category.Trim(),
            body.System,
            body.Phone,
            body.Company,
            cancellationToken);

        try
        {
            await email.SendChatNotificationEmailAsync(
                user.Identity?.Name ?? "Müşteri",
                user.FindFirstValue(ClaimTypes.Email) ?? "",
                $"{created.Subject}\n\n{created.Description}",
                created.ConversationId?.ToString() ?? created.Id.ToString(),
                created.CreatedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("SupportApi").LogWarning(ex, "Request notification email failed for {Id}", created.Id);
        }

        return Results.Json(created, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetRequestAsync(
        Guid id,
        ClaimsPrincipal user,
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var item = await requests.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (!user.IsInRole("admin") && !string.Equals(item.CustomerId, userId, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        return Results.Json(item);
    }

    private static async Task<IResult> GetConversationAsync(
        Guid id,
        ClaimsPrincipal user,
        IChatStore chat,
        CancellationToken cancellationToken)
    {
        var conversation = await chat.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound();
        }

        if (!CanAccess(user, conversation.CustomerId))
        {
            return Results.Forbid();
        }

        return Results.Json(conversation);
    }

    private static async Task<IResult> ListMessagesAsync(
        Guid id,
        ClaimsPrincipal user,
        IChatStore chat,
        CancellationToken cancellationToken)
    {
        var conversation = await chat.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound();
        }

        if (!CanAccess(user, conversation.CustomerId))
        {
            return Results.Forbid();
        }

        return Results.Json(await chat.ListMessagesAsync(id, cancellationToken));
    }

    private static async Task<IResult> SendMessageAsync(
        Guid id,
        MessageBody body,
        ClaimsPrincipal user,
        IChatStore chat,
        ISupabaseAccountService accounts,
        IResendEmailService email,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(body.Body))
        {
            return Results.BadRequest(new { error = "Mesaj gerekli." });
        }

        var conversation = await chat.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound();
        }

        var isAdmin = user.IsInRole("admin");
        if (!isAdmin && !string.Equals(conversation.CustomerId, userId, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        var role = isAdmin ? "admin" : "customer";
        var sent = await chat.SendAsync(id, userId, role, body.Body, cancellationToken);

        if (!isAdmin)
        {
            try
            {
                var profile = await accounts.GetProfileAsync(userId, cancellationToken);
                await email.SendChatNotificationEmailAsync(
                    profile?.FullName ?? user.Identity?.Name ?? "Müşteri",
                    profile?.Email ?? user.FindFirstValue(ClaimTypes.Email) ?? "",
                    sent.Body,
                    id.ToString(),
                    sent.CreatedAt,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("SupportApi").LogWarning(ex, "Chat notification email failed for {Conversation}", id);
            }
        }

        return Results.Json(new
        {
            id = sent.Id,
            conversationId = sent.ConversationId,
            senderRole = sent.SenderRole,
            body = sent.Body,
            createdAt = sent.CreatedAt,
            readAt = sent.ReadAt
        });
    }

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        ClaimsPrincipal user,
        IChatStore chat,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var message = await chat.GetMessageAsync(id, cancellationToken);
        if (message is null)
        {
            return Results.NotFound();
        }

        var conversation = await chat.GetAsync(message.ConversationId, cancellationToken);
        if (conversation is null || !CanAccess(user, conversation.CustomerId))
        {
            return Results.Forbid();
        }

        await chat.MarkMessageReadAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListCustomersAsync(
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        return Results.Json(await requests.ListCustomersAsync(cancellationToken));
    }

    private static async Task<IResult> GetCustomerAsync(
        string id,
        ISupabaseAccountService accounts,
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        var customer = await accounts.GetProfileAsync(id, cancellationToken);
        if (customer is null)
        {
            return Results.NotFound();
        }

        var items = await requests.ListForCustomerAsync(id, cancellationToken);
        return Results.Json(new { customer, requests = items });
    }

    private static async Task<IResult> ListAdminRequestsAsync(
        string? status,
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        return Results.Json(await requests.ListForAdminAsync(status, cancellationToken));
    }

    private static async Task<IResult> ListAdminConversationsAsync(
        IChatStore chat,
        CancellationToken cancellationToken)
    {
        return Results.Json(await chat.ListForAdminAsync(cancellationToken));
    }

    private static bool CanAccess(ClaimsPrincipal user, string customerId)
    {
        if (user.IsInRole("admin"))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(userId) &&
               string.Equals(customerId, userId, StringComparison.Ordinal);
    }

    public sealed class MessageBody
    {
        public string Body { get; set; } = "";
    }

    public sealed class CreateRequestBody
    {
        public string Subject { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "genel";
        public string? System { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
    }
}
