using System.Security.Claims;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedSupport.Web.Api;

public static class SupportApi
{
    public static void MapSupportApi(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapPost("/conversations/{id:guid}/messages", SendMessageAsync);
        group.MapPost("/messages/{id:guid}/read", MarkReadAsync);
        group.MapGet("/requests", ListMyRequestsAsync);
        group.MapGet("/admin/customers", ListCustomersAsync).RequireAuthorization("AdminOnly");
        group.MapGet("/admin/requests", ListAdminRequestsAsync).RequireAuthorization("AdminOnly");
    }

    private static async Task<IResult> SendMessageAsync(
        Guid id,
        MessageBody body,
        ClaimsPrincipal user,
        IChatStore chat,
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
        return Results.Json(new
        {
            id = sent.Id,
            conversationId = sent.ConversationId,
            senderRole = sent.SenderRole,
            body = sent.Body,
            createdAt = sent.CreatedAt
        });
    }

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        ClaimsPrincipal user,
        IChatStore chat,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return Results.Unauthorized();
        }

        await chat.MarkMessageReadAsync(id, cancellationToken);
        return Results.NoContent();
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

        var items = await requests.ListForCustomerAsync(userId, cancellationToken);
        return Results.Json(items);
    }

    private static async Task<IResult> ListCustomersAsync(
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        return Results.Json(await requests.ListCustomersAsync(cancellationToken));
    }

    private static async Task<IResult> ListAdminRequestsAsync(
        string? status,
        ICustomerRequestStore requests,
        CancellationToken cancellationToken)
    {
        return Results.Json(await requests.ListForAdminAsync(status, cancellationToken));
    }

    public sealed class MessageBody
    {
        public string Body { get; set; } = "";
    }
}
