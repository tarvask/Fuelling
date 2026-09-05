using FuelStation.ReservationService.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FuelStation.IntegrationTests;

public class IntegrationTestFixture : IAsyncLifetime
{
    private const string GrpcServerAddress = "http://localhost";
    private const string ConnectionStringsDefault = "ConnectionStrings:Default";
    private const string RedisConnectionString = "Redis:ConnectionString";
    private const string KafkaBootstrapServersString = "Kafka:BootstrapServers";

    private KafkaContainer _kafkaContainer = null!;
    private PostgreSqlContainer _postgresContainer = null!;
    private RedisContainer _redisContainer = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string KafkaBootstrapServers { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // launch containers
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("fuelstation_test")
            .WithUsername("fuelapp")
            .WithPassword("fuelapp_secret")
            .Build();

        _redisContainer = new RedisBuilder()
            .Build();

        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.0")
            .Build();

        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _kafkaContainer.StartAsync()
        );

        KafkaBootstrapServers = _kafkaContainer.GetBootstrapAddress();

        // create fabric with overriden configuration
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [ConnectionStringsDefault] = _postgresContainer.GetConnectionString(),
                        [RedisConnectionString] = _redisContainer.GetConnectionString() + ",allowAdmin=true",
                        [KafkaBootstrapServersString] = _kafkaContainer.GetBootstrapAddress(),
                        
                        // speed up simulation a bit
                        ["Simulation:MinDeliveryDurationMinutes"] = "0",
                        ["Simulation:MaxDeliveryDurationMinutes"] = "0",
                        ["Simulation:MinUnloadDurationMinutes"] = "1",
                        ["Simulation:MaxUnloadDurationMinutes"] = "1",
                        ["Simulation:SpeedFactor"] = "60",
                        ["Simulation:MaxTankFillRetriesCount"] = "3",
                        ["Simulation:TankFillRetryDelayMs"] = "10"
                    });
                });

                builder.UseEnvironment("Testing");
            });

        // apply migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();

        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask(),
            _kafkaContainer.DisposeAsync().AsTask()
        );
    }

    public GrpcChannel CreateGrpcChannel()
    {
        var httpClient = Factory.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(GrpcServerAddress, new GrpcChannelOptions { HttpClient = httpClient });
        return channel;
    }
    
    public async Task ResetDatabaseAsync()
    {
        // clear Postgres db
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DeliveryCompartments.RemoveRange(db.DeliveryCompartments);
        db.DeliverySessions.RemoveRange(db.DeliverySessions);
        db.FuellingSessions.RemoveRange(db.FuellingSessions);
        db.Nozzles.RemoveRange(db.Nozzles);
        db.Pumps.RemoveRange(db.Pumps);
        db.Tanks.RemoveRange(db.Tanks);
        db.Stations.RemoveRange(db.Stations);
        await db.SaveChangesAsync();
        
        // clear Redis db
        var multiplexer = Factory.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
        var server = multiplexer.GetServer(multiplexer.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }
}