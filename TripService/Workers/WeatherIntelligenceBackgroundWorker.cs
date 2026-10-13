using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TripService.Data;
using TripService.Services;

namespace TripService.Workers;

public class WeatherIntelligenceBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WeatherIntelligenceBackgroundWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public WeatherIntelligenceBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<WeatherIntelligenceBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUpcomingTripsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error occurred during weather intelligence background cycle");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessUpcomingTripsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
        var weatherIntelligenceService = scope.ServiceProvider.GetRequiredService<IWeatherIntelligenceService>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var maxDepartDate = today.AddDays(14);

        var activeTripIds = await db.Trips
            .AsNoTracking()
            .Where(t => !t.IsArchived && t.DepartDate >= today && t.DepartDate <= maxDepartDate)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var tripId in activeTripIds)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await weatherIntelligenceService.CheckAndProcessTripAsync(tripId, force: false, ct: ct);
                if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    _logger.LogWarning("Weather intelligence processing failed for trip {TripId}: {Error}", tripId, result.ErrorMessage);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process weather intelligence for trip {TripId}", tripId);
            }
        }
    }
}
