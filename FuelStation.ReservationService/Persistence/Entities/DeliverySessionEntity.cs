namespace FuelStation.ReservationService.Persistence.Entities;

public class DeliverySessionEntity
{
    public string Id { get; set; } = "";
    public DeliverySessionStatus Status { get; set; } = DeliverySessionStatus.Scheduled;
    
    // foreign keys
    public string StationId { get; set; } = "";
    
    // nav fields
    public StationEntity Station { get; set; } = null!;
    public List<DeliveryCompartmentEntity> Compartments { get; set; } = new();
}

public enum DeliverySessionStatus
{
    Scheduled = 0,
    Arrived = 1,
    Completed = 2,
    Failed = 3
}