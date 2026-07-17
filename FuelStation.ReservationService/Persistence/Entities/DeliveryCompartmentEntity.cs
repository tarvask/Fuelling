using Fuel;

namespace FuelStation.ReservationService.Persistence.Entities;

public class DeliveryCompartmentEntity
{
    public string Id { get; set; } = "";
    public FuelType FuelType { get; set; }
    public double Litres { get; set; }

    // foreign keys
    public string DeliverySessionId { get; set; } = "";

    // nav fields
    public DeliverySessionEntity DeliverySession { get; set; } = null!;
}