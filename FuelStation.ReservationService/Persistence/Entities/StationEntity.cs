namespace FuelStation.ReservationService.Persistence.Entities;

public class StationEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    
    public List<TankEntity> Tanks { get; set; } = new();
    public List<PumpEntity> Pumps { get; set; } = new();
}