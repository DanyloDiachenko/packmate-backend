using System;

namespace TripItemsService.Entities;

public class TripItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }

    public string Section { get; set; }
    public string Title { get; set; } = String.Empty;
    public int Quantity { get; set; } = 1;
    public bool IsTaken { get; set; } = false;
    public string? Tag { get; set; } = "important";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}