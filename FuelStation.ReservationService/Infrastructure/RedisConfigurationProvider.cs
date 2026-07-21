namespace FuelStation.ReservationService.Infrastructure;

public class RedisConfigurationProvider
{
    public RedisConfigurationProvider(IConfiguration configuration)
    {
        ConnectionString =
            Environment.GetEnvironmentVariable("Redis__ConnectionString")
            ?? configuration.GetValue<string>("Redis:ConnectionString")
            ?? "localhost:6379";
    }

    public string ConnectionString { get; }
}