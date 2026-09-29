using Microsoft.EntityFrameworkCore;
using TripItemsService.Entities;

namespace TripItemsService.Data;

public class TripItemsDbContext : DbContext
{
    public TripItemsDbContext(DbContextOptions<TripItemsDbContext> options)
    : base(options) { }
    public DbSet<TripItem> TripItems => Set<TripItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TripItem>(entity =>
        {
            entity.ToTable("trip_items");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.TripId).IsRequired();
            entity.HasIndex(i => i.TripId);

            entity.Property(i => i.Section).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Title).HasMaxLength(255).IsRequired();
            entity.Property(i => i.Quantity).HasDefaultValue(1);
            entity.Property(i => i.IsTaken).HasDefaultValue(false);
            entity.Property(i => i.Tag).HasMaxLength(50);

            entity.Property(i => i.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}