using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Entities;
using NotificationService.Extensions;
using NotificationService.Services;

namespace NotificationService.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Notifications");

        group.MapGet("/", async (
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            ClaimsPrincipal userClaims,
            NotificationDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var query = db.Notifications
                .AsNoTracking()
                .Where(m => m.UserId == userId);

            var unreadCount = await query.CountAsync(m => !m.IsRead, ct);

            var effectiveLimit = limit is > 0 ? Math.Min(limit.Value, 100) : 20;
            var effectiveOffset = offset is > 0 ? offset.Value : 0;

            var items = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip(effectiveOffset)
                .Take(effectiveLimit)
                .Select(m => new UserNotificationItemDto(
                    m.Id,
                    m.Category,
                    m.Title,
                    m.Body,
                    new NotificationDataDto(m.TripId, m.ImpactId),
                    m.IsRead,
                    m.CreatedAt
                ))
                .ToListAsync(ct);

            return Results.Ok(new UserNotificationsListResponseDto(items, unreadCount));
        })
        .RequireAuthorization()
        .WithName("GetUserNotifications")
        .WithSummary("List user notifications")
        .WithDescription("Retrieves paginated notifications for the authenticated user, ordered by creation date descending, along with total unread count.")
        .Produces<UserNotificationsListResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}/read", async (
            Guid id,
            ClaimsPrincipal userClaims,
            NotificationDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var message = await db.Notifications.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
            if (message == null)
            {
                return Results.NotFound(new ErrorResponse("Notification not found"));
            }

            message.IsRead = true;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new UserNotificationItemDto(
                message.Id,
                message.Category,
                message.Title,
                message.Body,
                new NotificationDataDto(message.TripId, message.ImpactId),
                message.IsRead,
                message.CreatedAt
            ));
        })
        .RequireAuthorization()
        .WithName("MarkNotificationAsRead")
        .WithSummary("Mark notification as read")
        .WithDescription("Marks a specific user notification as read.")
        .Produces<UserNotificationItemDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/read-all", async (
            ClaimsPrincipal userClaims,
            NotificationDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var updatedCount = await db.Notifications
                .Where(m => m.UserId == userId && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);

            return Results.Ok(new MarkAllReadResponseDto(updatedCount));
        })
        .RequireAuthorization()
        .WithName("MarkAllNotificationsAsRead")
        .WithSummary("Mark all notifications as read")
        .WithDescription("Marks all unread notifications for the authenticated user as read.")
        .Produces<MarkAllReadResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", async (
            CreateNotificationRequestDto request,
            NotificationDbContext db,
            IPushNotificationService pushService,
            ClaimsPrincipal? userClaims,
            CancellationToken ct
        ) =>
        {
            var targetUserId = request.UserId;
            if (targetUserId == Guid.Empty && userClaims?.Identity?.IsAuthenticated == true)
            {
                targetUserId = userClaims.GetUserId();
            }

            if (targetUserId == Guid.Empty)
            {
                return Results.BadRequest(new ErrorResponse("UserId is required"));
            }

            var notification = new NotificationMessage
            {
                UserId = targetUserId,
                TripId = request.TripId,
                Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category,
                Title = request.Title,
                Body = request.Body,
                ActionTitle = request.ActionTitle,
                RelatedDestination = request.RelatedDestination,
                ImpactId = request.ImpactId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            db.Notifications.Add(notification);
            await db.SaveChangesAsync(ct);

            if (request.SendPush)
            {
                await pushService.SendPushNotificationAsync(
                    notification.UserId,
                    notification.TripId,
                    notification.RelatedDestination,
                    notification.Id,
                    notification.Title,
                    notification.Body,
                    notification.Category,
                    ct
                );
            }

            var response = new UserNotificationItemDto(
                notification.Id,
                notification.Category,
                notification.Title,
                notification.Body,
                new NotificationDataDto(notification.TripId, notification.ImpactId),
                notification.IsRead,
                notification.CreatedAt
            );

            return Results.Created($"/notifications/{notification.Id}", response);
        })
        .WithName("CreateNotification")
        .WithSummary("Create a notification")
        .WithDescription("Creates a new in-app notification message and optionally dispatches an APNs push notification.")
        .Produces<UserNotificationItemDto>(StatusCodes.Status201Created)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return group;
    }
}
