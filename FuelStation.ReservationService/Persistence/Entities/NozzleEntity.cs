namespace FuelStation.ReservationService.Persistence.Entities;

public class NozzleEntity
{
    public string Id { get; set; } = "";
    public Fuel.FuelType FuelType { get; set; }
    public string TankId { get; set; } = "";
    
    public TankEntity Tank { get; set; } = null!;
    public string PumpId { get; set; } = "";
    public PumpEntity Pump { get; set; } = null!;
}