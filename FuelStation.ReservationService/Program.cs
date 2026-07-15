using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Services;
using FuelStation.ReservationService.Models;
using FuelStation.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Services.Configure<StationConfig>(builder.Configuration.GetSection("Station"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StationConfig>>().Value);
builder.Services.AddSingleton<ReservationManager>();
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddGrpc();

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
app.Run();