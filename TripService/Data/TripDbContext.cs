using Microsoft.EntityFrameworkCore;
using TripService.Entities;

namespace TripService.Data;

public class TripDbContext : DbContext
{
    public TripDbContext(DbContextOptions<TripDbContext> options) : base(options) { }

    public DbSet<Trip> Trips => Set<Trip>();

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

            entity.OwnsOne(t => t.Destination, destBuilder =>
            {
                destBuilder.ToJson();
            });

            entity.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}