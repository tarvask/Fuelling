using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using FuelStation.Shared.Constants;
using FuelStation.Shared.Models;
using FuelStation.SupplyServiceUI.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace FuelStation.SupplyServiceUI.Services;

public class DeliveryEventConsumerService : BackgroundService
{
    private readonly DeliveryEventBus _eventBus;
    private readonly string _bootstrapServers;

    public DeliveryEventConsumerService(DeliveryEventBus eventBus, AppConfigProvider configProvider)
    {
        _eventBus = eventBus;
        _bootstrapServers = configProvider.KafkaBootstrapServers;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = KafkaGroups.SupplyServiceUI,
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(new[]
        {
            KafkaTopics.DeliveryEvents
        });
        
        Console.WriteLine($">>> [KafkaConsumer] Subscribed to {KafkaTopics.DeliveryEvents}");
        
        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(stoppingToken);

                    if (TryParseMessage(cr.Message, out var deliveryEvent) == false || deliveryEvent == null)
                        continue;
                    
                    Console.WriteLine($"[KafkaConsumer] Station {deliveryEvent.StationId} | {cr.Topic}: {cr.Message.Value}");
                    _eventBus.Publish(deliveryEvent);
                }
                catch (OperationCanceledException) { break; }
            }
            consumer.Close();
        }, stoppingToken);
    }

    private bool TryParseMessage(Message<string, string> message, out DeliveryEventMessage? deliveryEvent)
    {
        deliveryEvent = default;
        
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.Value);
        if (payload == null)
            return false;

        var stationId = payload[KafkaMessageKeys.StationId].GetString()!;
        var sessionId = payload[KafkaMessageKeys.SessionId].GetString()!;
        var deliveryStatusRaw = payload[KafkaMessageKeys.DeliveryStatus].GetString()!;
        var timestamp = payload[KafkaMessageKeys.Timestamp].GetDateTime();

        if (Enum.TryParse<DeliveryStatusType>(deliveryStatusRaw, out var deliveryStatus) == false)
            return false;

        deliveryEvent = new DeliveryEventMessage
        {
            SessionId = sessionId,
            StationId = stationId,
            DeliveryStatus = deliveryStatus,
            Timestamp = timestamp
        };
        return true;
    }
}