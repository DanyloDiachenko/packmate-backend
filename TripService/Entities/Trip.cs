using System;
using System.Collections.Generic;

namespace TripService.Entities;

public class Trip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public Destination Destination { get; set; } = default!;
    public DateOnly DepartDate { get; set; }
    public DateOnly ReturnDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsArchived { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}