using System;
using System.Collections.Generic;

namespace NotificationService.DTOs;

public record NotificationDataDto(
    Guid? TripId,
    Guid? ImpactId
);

public record UserNotificationItemDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    NotificationDataDto? Data,
    bool IsRead,
    DateTime CreatedAt
);

public record UserNotificationsListResponseDto(
    List<UserNotificationItemDto> Items,
    int UnreadCount
);

public record MarkAllReadResponseDto(
    int MarkedCount
);

public record CreateNotificationRequestDto(
    Guid UserId,
    Guid? TripId,
    string? Category,
    string Title,
    string Body,
    string? ActionTitle,
    string? RelatedDestination,
    Guid? ImpactId,
    bool SendPush = true
);

public record ErrorResponse(
    string Message
);
