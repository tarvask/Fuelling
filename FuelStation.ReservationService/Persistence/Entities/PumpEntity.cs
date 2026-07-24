namespace FuelStation.ReservationService.Persistence.Entities;

public class PumpEntity
{
    public string Id { get; set; } = "";
    
    // foreign keys
    public string StationId { get; set; } = "";
    
    // nav fields
    public StationEntity Station { get; set; } = null!;
    public List<NozzleEntity> Nozzles { get; set; } = new();
}