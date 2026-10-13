namespace WeatherService.Settings;

public class RedisSettings
{
    public string Configuration { get; init; } = "localhost:6379";
    public int CacheDurationHours { get; init; } = 4;
}
