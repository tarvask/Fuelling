using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Services;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.Shared.Constants;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables();
builder.Services.Configure<FuelNetworkConfig>(builder.Configuration.GetSection("FuelNetwork"));
builder.Services.Configure<SimulationConfig>(builder.Configuration.GetSection("Simulation"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<StationConfig>>().Value);
builder.Services.AddSingleton<ReservationManager>();
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddSingleton<DeliveryOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeliveryOrchestrator>());
builder.Services.AddSingleton<DbInitializerService>();
builder.Services.AddGrpc();
builder.Services.AddSingleton<KafkaConfigurationProvider>();
builder.Services.AddSingleton<PostgresConfigurationProvider>();
builder.Services.AddSingleton<RedisConfigurationProvider>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var pgConfig = serviceProvider.GetRequiredService<PostgresConfigurationProvider>();
    options.UseNpgsql(pgConfig.ConnectionString);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var provider = sp.GetRequiredService<RedisConfigurationProvider>();
    var config = ConfigurationOptions.Parse(provider.ConnectionString);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddSingleton<RedisLockProvider>();

builder.WebHost.ConfigureKestrel(options =>
{
    // gRPC port (only HTTP/2)
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    // Healthcheck + REST port (only HTTP/1.1)
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
});

var app = builder.Build();

// create Kafka topics before launching web host
var kafkaConfigProvider = app.Services.GetRequiredService<KafkaConfigurationProvider>();
var adminConfig = new AdminClientConfig { BootstrapServers = kafkaConfigProvider.BootstrapServers };
using (var adminClient = new AdminClientBuilder(adminConfig).Build())
{
    var topics = new[]
    {
        KafkaTopics.FuellingStarted,
        KafkaTopics.FuellingCompleted,
        KafkaTopics.DeliveryEvents
    };
    var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
    var existing = metadata.Topics.Select(t => t.Topic).ToHashSet();
    var toCreate = topics.Where(t => existing.Contains(t) == false)
        .Select(t => new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 })
        .ToList();
    if (toCreate.Any())
        await adminClient.CreateTopicsAsync(toCreate);
}

app.MapGrpcService<FuelReservationService>();
app.MapGet("/", () => "ReservationService is running");

Console.WriteLine("ReservationService gRPC + HTTP on http://localhost:5001");

using var scope = app.Services.CreateScope();
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    var fuelNetworkConfig = scope.ServiceProvider.GetRequiredService<IOptions<FuelNetworkConfig>>().Value;
    var dbInitService = scope.ServiceProvider.GetRequiredService<DbInitializerService>();
    await dbInitService.InitDbFromConfig(db, fuelNetworkConfig);
}

app.Run();