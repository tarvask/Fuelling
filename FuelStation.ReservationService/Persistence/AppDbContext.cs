using Microsoft.EntityFrameworkCore;
using FuelStation.ReservationService.Persistence.Entities;

namespace FuelStation.ReservationService.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TankEntity> Tanks => Set<TankEntity>();
    public DbSet<PumpEntity> Pumps => Set<PumpEntity>();
    public DbSet<NozzleEntity> Nozzles => Set<NozzleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TankEntity>(entity =>
        {
            entity.ToTable("tanks");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.FuelType)
                .HasConversion<string>()
                .HasMaxLength(8);
            entity.Property(t => t.CurrentVolume).HasPrecision(10, 2);
            entity.Property(t => t.Capacity).HasPrecision(10, 2);
        });

        modelBuilder.Entity<PumpEntity>(entity =>
        {
            entity.ToTable("pumps");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.IsBusy).IsRequired();
        });

        modelBuilder.Entity<NozzleEntity>(entity =>
        {
            entity.ToTable("nozzles");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.FuelType)
                .HasConversion<string>()
                .HasMaxLength(8);

            entity.HasOne(n => n.Tank)
                .WithMany()
                .HasForeignKey(n => n.TankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Pump)
                .WithMany(p => p.Nozzles)
                .HasForeignKey(n => n.PumpId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}