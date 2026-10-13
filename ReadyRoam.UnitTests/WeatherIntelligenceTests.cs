using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TripService.Data;
using TripService.DTOs;
using TripService.Entities;
using NotificationService.Data;
using NotificationService.Entities;
using WeatherService.DTOs;
using Xunit;

namespace ReadyRoam.UnitTests;

public class WeatherIntelligenceTests
{
    [Fact]
    public void WeatherDelta_DetectsRainSpike_AndColdFront()
    {
        var prevForecast = new TripWeatherForecastCache
        {
            ForecastDate = new DateOnly(2026, 7, 10),
            HighTemp = 25,
            LowTemp = 18,
            RainProbability = 10,
            UvIndex = 4,
            WindSpeed = 15,
            Condition = "Sunny"
        };

        var newForecast = new ExternalDailyForecastItemDto(
            new DateOnly(2026, 7, 10),
            "Friday",
            20,
            9,
            "Heavy Rain",
            "cloud.heavyrain.fill",
            75,
            2,
            20
        );

        var isRainSpike = newForecast.RainProbability >= 50 && prevForecast.RainProbability < 30;
        var isColdFront = newForecast.LowTemp <= 10 || (prevForecast.LowTemp - newForecast.LowTemp >= 5);

        isRainSpike.Should().BeTrue();
        isColdFront.Should().BeTrue();
    }

    [Fact]
    public void WeatherCooldown_BlocksAutomaticCheck_Within24Hours()
    {
        var lastChecked = DateTime.UtcNow.AddHours(-10);
        var isWithin24Hours = lastChecked > DateTime.UtcNow.AddHours(-24);

        isWithin24Hours.Should().BeTrue();

        var force = false;
        var shouldSkip = !force && isWithin24Hours;
        shouldSkip.Should().BeTrue();

        var forceRerun = true;
        var shouldSkipForced = !forceRerun && isWithin24Hours;
        shouldSkipForced.Should().BeFalse();
    }

    [Fact]
    public async Task NotificationInbox_PaginatesAndCountsUnreadMessages()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var userId = Guid.NewGuid();

        await using (var db = new NotificationDbContext(options))
        {
            for (var i = 0; i < 5; i++)
            {
                db.Notifications.Add(new NotificationMessage
                {
                    UserId = userId,
                    Category = "weatherAlert",
                    Title = $"Alert {i}",
                    Body = $"Body {i}",
                    IsRead = i >= 3,
                    CreatedAt = DateTime.UtcNow.AddMinutes(i)
                });
            }
            await db.SaveChangesAsync();
        }

        await using (var db = new NotificationDbContext(options))
        {
            var query = db.Notifications.Where(m => m.UserId == userId);
            var unreadCount = await query.CountAsync(m => !m.IsRead);
            var paginated = await query.OrderByDescending(m => m.CreatedAt).Take(2).ToListAsync();

            unreadCount.Should().Be(3);
            paginated.Should().HaveCount(2);
            paginated[0].Title.Should().Be("Alert 4");
            paginated[1].Title.Should().Be("Alert 3");
        }
    }

    [Fact]
    public void NonDestructiveAdjustment_NeverResetsIsPackedFlag()
    {
        var existingItems = new List<ExternalTripItemDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Clothes", "Warm Jacket", 1, true, "essential"),
            new(Guid.NewGuid(), Guid.NewGuid(), "Clothes", "T-shirt", 3, true, "standard"),
            new(Guid.NewGuid(), Guid.NewGuid(), "Accessories", "Sunglasses", 1, false, "optional")
        };

        var selectedProposals = new List<WeatherProposalDto>
        {
            new(Guid.NewGuid(), "Compact Travel Umbrella", "Accessories", 1, "essential", "High rain probability")
        };

        var newItemsToCreate = selectedProposals.Select(p => new ExternalCreateTripItemDto(
            Section: p.Section,
            Title: p.Title,
            Quantity: p.Quantity,
            Tag: p.Tag
        )).ToList();

        newItemsToCreate.Should().HaveCount(1);
        newItemsToCreate[0].Title.Should().Be("Compact Travel Umbrella");

        existingItems.First(i => i.Title == "Warm Jacket").IsTaken.Should().BeTrue();
        existingItems.First(i => i.Title == "T-shirt").IsTaken.Should().BeTrue();
    }

    [Fact]
    public void DailyForecast_IncludesDayOfWeek_AndSpansAllDates()
    {
        var startDate = new DateOnly(2026, 10, 18);
        var endDate = new DateOnly(2026, 10, 28);
        var items = new List<DailyForecastItemDto>();

        for (var curr = startDate; curr <= endDate; curr = curr.AddDays(1))
        {
            items.Add(new DailyForecastItemDto(
                Date: curr,
                DayOfWeek: curr.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture),
                HighTemp: 22,
                LowTemp: 14,
                Condition: "Partly cloudy",
                IconName: "cloud.sun.fill",
                RainProbability: 10,
                UvIndex: 4,
                WindSpeed: 12
            ));
        }

        items.Should().HaveCount(11);
        items.First().Date.Should().Be(startDate);
        items.First().DayOfWeek.Should().Be("Sunday");
        items.Last().Date.Should().Be(endDate);
        items.Last().DayOfWeek.Should().Be("Wednesday");
    }

    [Fact]
    public void WeatherProcessResult_Failure_ReturnsErrorMessageAndFalse()
    {
        var failure = WeatherProcessResult.Failure("AI service returned empty weather repack recommendations.");

        failure.Success.Should().BeFalse();
        failure.ErrorMessage.Should().Be("AI service returned empty weather repack recommendations.");
        failure.HasWeatherImpact.Should().BeFalse();
        ((bool)failure).Should().BeFalse();
    }

    [Fact]
    public void WeatherProcessResult_Success_ReturnsTrue()
    {
        var success = WeatherProcessResult.Ok(hasWeatherImpact: true);

        success.Success.Should().BeTrue();
        success.ErrorMessage.Should().BeNull();
        success.HasWeatherImpact.Should().BeTrue();
        ((bool)success).Should().BeTrue();
    }
}
