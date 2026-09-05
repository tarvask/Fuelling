using Grpc.Net.Client;
using Fuel;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;

namespace FuelStation.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<IntegrationTestFixture>
{
    protected IntegrationTestFixture Fixture { get; }
    protected GrpcChannel Channel { get; }
    protected FuelReservation.FuelReservationClient Client { get; }

    public IntegrationTestBase(IntegrationTestFixture fixture)
    {
        Fixture = fixture;
        Channel = fixture.CreateGrpcChannel();
        Client = new FuelReservation.FuelReservationClient(Channel);
    }
    
    protected static void CreatePump(AppDbContext db, string stationId, string tankId, string pumpId, FuelType fuelType)
    {
        var pump = new PumpEntity { Id = pumpId, StationId = stationId };
        var nozzle = new NozzleEntity
        {
            Id = Guid.NewGuid().ToString(),
            FuelType = fuelType,
            TankId = tankId,
            PumpId = pump.Id
        };
        pump.Nozzles.Add(nozzle);
        db.Pumps.Add(pump);
        db.Nozzles.Add(nozzle);
    }
}