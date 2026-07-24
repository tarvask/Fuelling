using Microsoft.EntityFrameworkCore;
using FuelStation.ReservationService.Persistence.Entities;

namespace FuelStation.ReservationService.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<StationEntity> Stations => Set<StationEntity>();
    public DbSet<TankEntity> Tanks => Set<TankEntity>();
    public DbSet<PumpEntity> Pumps => Set<PumpEntity>();
    public DbSet<NozzleEntity> Nozzles => Set<NozzleEntity>();

    public DbSet<FuellingSessionEntity> FuellingSessions => Set<FuellingSessionEntity>();
    public DbSet<DeliverySessionEntity> DeliverySessions => Set<DeliverySessionEntity>();
    public DbSet<DeliveryCompartmentEntity> DeliveryCompartments => Set<DeliveryCompartmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TankEntity>(entity =>
        {
            entity.ToTable("tanks");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.FuelType)
                .HasConversion<string>()
                .HasMaxLength(8)
                .IsRequired();
            entity.Property(t => t.CurrentVolume).HasPrecision(10, 2);
            entity.Property(t => t.Capacity).HasPrecision(10, 2);
            
            entity.HasOne(t => t.Station)
                .WithMany(g => g.Tanks)
                .HasForeignKey(t => t.StationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PumpEntity>(entity =>
        {
            entity.ToTable("pumps");
            entity.HasKey(p => p.Id);
            
            entity.HasOne(t => t.Station)
                .WithMany(g => g.Pumps)
                .HasForeignKey(t => t.StationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NozzleEntity>(entity =>
        {
            entity.ToTable("nozzles");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.FuelType)
                .HasConversion<string>()
                .HasMaxLength(8)
                .IsRequired();

            entity.HasOne(n => n.Tank)
                .WithMany()
                .HasForeignKey(n => n.TankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Pump)
                .WithMany(p => p.Nozzles)
                .HasForeignKey(n => n.PumpId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FuellingSessionEntity>(entity =>
        {
            entity.ToTable("fuelling_sessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.FuelType)
                .HasConversion<string>()
                .HasMaxLength(8)
                .IsRequired();
            entity.Property(s => s.PumpId).IsRequired().HasMaxLength(50);
            entity.Property(s => s.TankId).IsRequired().HasMaxLength(50);
            entity.Property(s => s.ReservedVolume).HasPrecision(10, 2).IsRequired();
            entity.Property(s => s.ActualVolume).HasPrecision(10, 2);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);
            
            entity.HasOne(t => t.Station)
                .WithMany()
                .HasForeignKey(t => t.StationId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(s => s.Tank)
                .WithMany()
                .HasForeignKey(s => s.TankId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(s => s.Pump)
                .WithMany()
                .HasForeignKey(s => s.PumpId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<DeliverySessionEntity>(entity =>
        {
            entity.ToTable("delivery_sessions");
            entity.HasKey(e => e.Id);
            
            entity.HasOne(t => t.Station)
                .WithMany()
                .HasForeignKey(t => t.StationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<DeliveryCompartmentEntity>(entity =>
        {
            entity.ToTable("delivery_compartments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FuelType)
                .HasConversion<string>()
                .HasMaxLength(8)
                .IsRequired();

            entity.Property(e => e.Litres).IsRequired();
            
            entity.HasOne(e => e.DeliverySession)
                .WithMany(s => s.Compartments)
                .HasForeignKey(e => e.DeliverySessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StationEntity>(entity =>
        {
            entity.ToTable("stations");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
        });
    }
}