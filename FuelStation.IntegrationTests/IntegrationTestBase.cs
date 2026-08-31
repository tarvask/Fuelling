using Grpc.Net.Client;
using Fuel;

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
}