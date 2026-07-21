namespace FuelStation.ReservationService.Infrastructure;

public class PostgresConfigurationProvider
{
    public PostgresConfigurationProvider(IConfiguration configuration)
    {
        ConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=fuelstation;Username=fuelapp;Password=fuelapp_secret";
    }

    public string ConnectionString { get; }
}