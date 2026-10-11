

using Microsoft.EntityFrameworkCore;
using PresetService.Entities;

namespace PresetService.Data;


public class PresetDbContext : DbContext
{

    public PresetDbContext(DbContextOptions<PresetDbContext> options) : base(options)
    {
    }

    public DbSet<Preset> Presets => Set<Preset>();
    public DbSet<PresetItem> PresetItems => Set<PresetItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Preset>(entity =>
        {
            entity.ToTable("presets");
            entity.HasKey(x => x.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.HasIndex(e => e.UserId);

            entity.HasMany(e => e.Items)
                .WithOne(i => i.Preset)
                .HasForeignKey(i => i.PresetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PresetItem>(entity =>
        {
            entity.ToTable("preset_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Section).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Quantity).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.Tag).HasMaxLength(50);
            entity.HasIndex(e => new { e.PresetId, e.Section });
        });
    }
}