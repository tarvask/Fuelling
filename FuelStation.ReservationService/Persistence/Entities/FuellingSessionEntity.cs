namespace FuelStation.ReservationService.Persistence.Entities;

public class FuellingSessionEntity
{
    public string Id { get; set; } = "";
    public Fuel.FuelType FuelType { get; set; }
    public decimal ReservedVolume { get; set; }
    public decimal? ActualVolume { get; set; }
    public string Status { get; set; } = "";
    
    // foreign keys
    public string StationId { get; set; } = "";
    public string PumpId { get; set; } = "";
    public string TankId { get; set; } = "";

    // nav fields
    public StationEntity Station { get; set; } = null!;
    public PumpEntity Pump { get; set; } = null!;
    public TankEntity Tank { get; set; } = null!;
}