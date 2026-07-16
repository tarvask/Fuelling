namespace FuelStation.ReservationService.Persistence.Entities;

public class PumpEntity
{
    public string Id { get; set; } = "";
    public bool IsBusy { get; set; }          // temporary, to replace for Redis-flag
    public List<NozzleEntity> Nozzles { get; set; } = new();
}