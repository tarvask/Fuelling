using FuelStation.ReservationService.Persistence.Entities;
using Prometheus;

namespace FuelStation.ReservationService.Metrics;

public static class FuelStationMetrics
{
    public static readonly Counter FuellingStarted = Prometheus.Metrics
        .CreateCounter("fuelstation_fuelling_started_total", "Total number of fuelling sessions started.");

    public static readonly Counter FuellingCompleted = Prometheus.Metrics
        .CreateCounter("fuelstation_fuelling_completed_total", "Total number of fuelling sessions completed.");

    public static readonly Counter DeliveryStarted = Prometheus.Metrics
        .CreateCounter("fuelstation_delivery_started_total", "Total number of delivery sessions started.");

    public static readonly Counter DeliveryCompleted = Prometheus.Metrics
        .CreateCounter("fuelstation_delivery_completed_total", "Total number of delivery sessions completed.");

    public static readonly Counter Errors = Prometheus.Metrics
        .CreateCounter("fuelstation_errors_total", "Total number of business errors.", new CounterConfiguration
        {
            LabelNames = new[] { "station_id", "operation", "reason" }
        });
    
    public static readonly Gauge TankVolume = Prometheus.Metrics
        .CreateGauge("fuelstation_tank_volume_litres", "Current fuel volume in a tank.", new GaugeConfiguration
        {
            LabelNames = new[] { "station_id", "tank_id", "fuel_type" }
        });
    
    public static void UpdateTankVolumeMetric(TankEntity tank)
    {
        TankVolume
            .WithLabels(tank.StationId, tank.Id, $"{tank.FuelType}")
            .Set((double)tank.CurrentVolume);
    }
}