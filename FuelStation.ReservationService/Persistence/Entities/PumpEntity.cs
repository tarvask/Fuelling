namespace FuelStation.ReservationService.Persistence.Entities;

public class PumpEntity
{
    public string Id { get; set; } = "";
    
    // nav fields
    public List<NozzleEntity> Nozzles { get; set; } = new();
}