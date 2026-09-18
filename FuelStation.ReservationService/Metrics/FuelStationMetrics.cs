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
    
    public static readonly Histogram FuellingDuration = Prometheus.Metrics
        .CreateHistogram("fuelstation_fuelling_duration_seconds",
            "Duration of a fuelling session.",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.5, 1, 2, 5, 10, 30, 60 }
            });
    
    public static readonly Histogram DeliveryDuration = Prometheus.Metrics
        .CreateHistogram("fuelstation_delivery_duration_seconds",
            "Duration of a delivery session.",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.5, 1, 2, 5, 10, 30, 60 }
            });
    
    public static void UpdateTankVolumeMetric(TankEntity tank)
    {
        TankVolume
            .WithLabels(tank.StationId, tank.Id, $"{tank.FuelType}")
            .Set((double)tank.CurrentVolume);
    }
}