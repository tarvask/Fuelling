using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FuelStation.ReservationService.Services;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Services.Configure<StationConfig>(builder.Configuration.GetSection("Station"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<StationConfig>>().Value);
builder.Services.AddSingleton<ReservationManager>();
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddSingleton<DbInitializerService>();
builder.Services.AddGrpc();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

// create Kafka topics before launching web host
using (var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = "localhost:9092" }).Build())
{
    var topics = new[]
    {
        KafkaTopics.FuellingStarted,
        KafkaTopics.FuellingCompleted,
        KafkaTopics.FuelAddedFast,
        KafkaTopics.DeliveryStarted,
        KafkaTopics.DeliveryCompleted
    };
    var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
    var existing = metadata.Topics.Select(t => t.Topic).ToHashSet();
    var toCreate = topics.Where(t => !existing.Contains(t)).Select(t => new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 }).ToList();
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
    var stationConfig = scope.ServiceProvider.GetRequiredService<IOptions<StationConfig>>().Value;
    var dbInitService = scope.ServiceProvider.GetRequiredService<DbInitializerService>();
    await dbInitService.InitDbFromConfig(db, stationConfig);
}

app.Run();