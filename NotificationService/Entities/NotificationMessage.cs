using System;

namespace NotificationService.Entities;

public class NotificationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? TripId { get; set; }
    public string Category { get; set; } = "weatherAlert";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionTitle { get; set; }
    public string? RelatedDestination { get; set; }
    public Guid? ImpactId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
