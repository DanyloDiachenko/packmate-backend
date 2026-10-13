using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TripService.DTOs;
using TripService.Entities;

namespace TripService.Data;

public class TripDbContext : DbContext
{
    public TripDbContext(DbContextOptions<TripDbContext> options) : base(options) { }

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripWeatherForecastCache> TripWeatherForecasts => Set<TripWeatherForecastCache>();
    public DbSet<TripWeatherImpact> TripWeatherImpacts => Set<TripWeatherImpact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.ToTable("trips");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id).IsRequired();
            entity.HasIndex(t => t.UserId);

            entity.Property(t => t.Tags).IsRequired();
            entity.Property(t => t.DepartDate).IsRequired();
            entity.Property(t => t.ReturnDate).IsRequired();
            entity.Property(t => t.IsArchived).HasDefaultValue(false);
            entity.Property(t => t.HasWeatherImpact).HasDefaultValue(false);
            entity.Property(t => t.LastWeatherCheckAt);

            entity.OwnsOne(t => t.Destination, destBuilder =>
            {
                destBuilder.ToJson();
            });

            entity.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<TripWeatherForecastCache>(entity =>
        {
            entity.ToTable("trip_weather_forecasts");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.TripId).IsRequired();
            entity.Property(f => f.ForecastDate).IsRequired();
            entity.Property(f => f.Condition).HasMaxLength(64).IsRequired();
            entity.Property(f => f.IconName).HasMaxLength(64).IsRequired();
            entity.Property(f => f.RainProbability).HasDefaultValue(0);
            entity.Property(f => f.UvIndex).HasDefaultValue(0);
            entity.Property(f => f.WindSpeed).HasDefaultValue(0);
            entity.Property(f => f.FetchedAt).HasDefaultValueSql("NOW()");

            entity.HasIndex(f => new { f.TripId, f.ForecastDate }).IsUnique();

            entity.HasOne(f => f.Trip)
                .WithMany()
                .HasForeignKey(f => f.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TripWeatherImpact>(entity =>
        {
            entity.ToTable("trip_weather_impacts");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.TripId).IsRequired();
            entity.Property(i => i.DetectedAt).HasDefaultValueSql("NOW()");
            entity.Property(i => i.IsResolved).HasDefaultValue(false);

            entity.Property(i => i.Highlights)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<WeatherHighlightDto>>(v, (JsonSerializerOptions?)null) ?? new List<WeatherHighlightDto>()
                );

            entity.Property(i => i.Proposals)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<WeatherProposalDto>>(v, (JsonSerializerOptions?)null) ?? new List<WeatherProposalDto>()
                );

            entity.HasIndex(i => i.TripId);

            entity.HasOne(i => i.Trip)
                .WithMany()
                .HasForeignKey(i => i.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}