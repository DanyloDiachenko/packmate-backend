using TripService.DTOs;

namespace TripService.Infrastructure;

public interface INotificationServiceClient
{
    Task SendAsync(ExternalCreateNotificationRequestDto notification, CancellationToken ct = default);
}

public class NotificationServiceClient : INotificationServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NotificationServiceClient> _logger;

    public NotificationServiceClient(HttpClient http, ILogger<NotificationServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task SendAsync(ExternalCreateNotificationRequestDto notification, CancellationToken ct = default)
    {
        try
        {
            await _http.PostAsJsonAsync("/notifications", notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch notification for trip {TripId}", notification.TripId);
        }
    }
}
