using Fuel;

namespace FuelStation.ReservationService.Models;

public class TankState
{
    public string Id { get; init; } = "";
    public FuelType FuelType { get; init; }
    public decimal CurrentVolume { get; set; }
    public decimal Capacity { get; init; } 
}

public class FuellingSessionState
{
    public string Id { get; init; } = "";
    public string PumpId { get; init; } = "";
    public FuelType FuelType { get; set; }
    public string TankId { get; init; } = "";
    public decimal ReservedVolume { get; init; }
    public decimal? ActualVolume { get; set; }
    public string Status { get; set; } = "";
}

public class PumpState
{
    public string Id { get; init; } = "";
    public List<NozzleState> Nozzles { get; init; } = new();
    public bool IsBusy { get; set; }
}

public class NozzleState
{
    public FuelType FuelType { get; init; }
    public string TankId { get; init; } = "";
}

public class DeliverySessionState
{
    public List<Compartment> Compartments { get; init; } = new();
}