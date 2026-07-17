namespace FuelStation.ReservationService.Persistence.Entities;

public class PumpEntity
{
    public string Id { get; set; } = "";
    public bool IsBusy { get; set; }          // temporary, to replace for Redis-flag
    
    // nav fields
    public List<NozzleEntity> Nozzles { get; set; } = new();
}