namespace FuelStation.ReservationService.Persistence.Entities;

public class DeliverySessionEntity
{
    public string Id { get; set; } = "";
    
    // nav fields
    public List<DeliveryCompartmentEntity> Compartments { get; set; } = new();
}