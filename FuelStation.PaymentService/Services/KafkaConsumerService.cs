using FuelStation.PaymentService.Infrastructure;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using System.Text.Json;
using FuelStation.PaymentService.Models;
using FuelStation.Shared.Constants;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace FuelStation.PaymentService.Services;

public class KafkaConsumerService : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new(OpenTelemetryConstants.PaymentServiceSource);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly string _bootstrapServers;
    private readonly PaymentProcessingService _paymentService;

    public KafkaConsumerService(KafkaConfigurationProvider kafkaConfigProvider, PaymentProcessingService paymentService)
    {
        _bootstrapServers = kafkaConfigProvider.BootstrapServers;
        _paymentService = paymentService;
        Console.WriteLine($"[KafkaConsumerService] Kafka bootstrap servers: {_bootstrapServers}");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = KafkaGroups.PaymentService,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(new[]
        {
            KafkaTopics.FuellingCompleted,
        });
        
        return Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Consume(consumer, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // normal termination
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PaymentService error: {ex.Message}");
                    // to avoid spamming on serious errors
                    await Task.Delay(1000, stoppingToken);
                }
            }
            consumer.Close();
        }, stoppingToken);
    }

    private void Consume(IConsumer<string, string> consumer, CancellationToken stoppingToken)
    {
        var cr = consumer.Consume(stoppingToken);

        var parentContext = Propagator.Extract(
            default,
            cr.Message.Headers,
            (headers, key) =>
            {
                var bytes = headers.TryGetLastBytes(key, out var value) ? value : null;
                return bytes != null ? new[] { Encoding.UTF8.GetString(bytes) } : Enumerable.Empty<string>();
            });

        Baggage.Current = parentContext.Baggage;

        var fuelEvent = JsonSerializer.Deserialize<FuelingCompletedEvent>(cr.Message.Value);
        if (fuelEvent == null || string.IsNullOrEmpty(fuelEvent.StationId) || string.IsNullOrEmpty(fuelEvent.SessionId))
        {
            Console.WriteLine($">>> ERROR: invalid message payload: {cr.Message.Value}");
            consumer.Commit(cr);   // commit to avoid getting stuck at broken message
            return;
        }

        using var activity = ActivitySource.StartActivity(
            OpenTelemetryConstants.PaymentServiceActivity,
            ActivityKind.Consumer,
            parentContext.ActivityContext);
        
        if (activity != null)
        {
            // Add baggage items as span tags for easier filtering in Jaeger
            foreach (var item in Baggage.Current)
            {
                activity.SetTag($"{OpenTelemetryConstants.BaggageKeys.BaggagePrefix}{item.Key}", item.Value);
            }
            
            activity.SetTag(OpenTelemetryConstants.SpanKeys.SessionId, fuelEvent.SessionId);
            activity.SetTag(OpenTelemetryConstants.SpanKeys.StationId, fuelEvent.StationId);
            activity.SetTag(OpenTelemetryConstants.SpanKeys.FuelType, fuelEvent.FuelType);
            activity.SetTag(OpenTelemetryConstants.SpanKeys.FuelLitres, fuelEvent.Litres);
        }

        _paymentService.ProcessPayment(fuelEvent);
        consumer.Commit(cr);
    }
}