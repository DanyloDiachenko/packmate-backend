using System;

namespace TripService.Entities;

public class TripWeatherForecastCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public DateOnly ForecastDate { get; set; }
    public int HighTemp { get; set; }
    public int LowTemp { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public int RainProbability { get; set; }
    public int UvIndex { get; set; }
    public int WindSpeed { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
