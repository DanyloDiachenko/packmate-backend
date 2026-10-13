using System;
using System.Collections.Generic;
using TripService.DTOs;

namespace TripService.Entities;

public class TripWeatherImpact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }

    public List<WeatherHighlightDto> Highlights { get; set; } = new();
    public List<WeatherProposalDto> Proposals { get; set; } = new();
    public string? ExistingMatchedNotice { get; set; }
}
