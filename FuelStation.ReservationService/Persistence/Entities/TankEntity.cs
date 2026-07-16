namespace FuelStation.ReservationService.Persistence.Entities;

public class TankEntity
{
    public string Id { get; set; } = "";
    public Fuel.FuelType FuelType { get; set; }
    public decimal Capacity { get; set; }
    public decimal CurrentVolume { get; set; }
}