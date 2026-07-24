namespace FuelStation.ReservationService.Persistence.Entities;

public class DeliverySessionEntity
{
    public string Id { get; set; } = "";
    
    // foreign keys
    public string StationId { get; set; } = "";
    
    // nav fields
    public StationEntity Station { get; set; } = null!;
    public List<DeliveryCompartmentEntity> Compartments { get; set; } = new();
}