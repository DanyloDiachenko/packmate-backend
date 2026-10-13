using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NotificationService.Services;

public interface IPushNotificationService
{
    Task SendPushNotificationAsync(Guid userId, Guid? tripId, string? destination, Guid notificationId, string title, string body, string? category = null, CancellationToken ct = default);
}

public class PushNotificationService : IPushNotificationService
{
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(ILogger<PushNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendPushNotificationAsync(Guid userId, Guid? tripId, string? destination, Guid notificationId, string title, string body, string? category = null, CancellationToken ct = default)
    {
        var apnsPayload = new
        {
            aps = new
            {
                alert = new
                {
                    title = title,
                    body = body
                },
                badge = 1,
                sound = "default",
                category = category ?? "WEATHER_REPACK_CATEGORY"
            },
            data = new
            {
                type = category ?? "weather_repack",
                tripId = tripId?.ToString(),
                destination = destination,
                notificationId = notificationId.ToString()
            }
        };

        var json = JsonSerializer.Serialize(apnsPayload, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("Dispatched APNs push notification for user {UserId}:\n{Payload}", userId, json);

        return Task.CompletedTask;
    }
}
