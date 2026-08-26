using System.Text.Json;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using FuelStation.ReservationService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FuelStation.Tests;

public static class TestHelpers
{
    public static (ServiceProvider serviceProvider, IServiceScopeFactory scopeFactory,
        IRedisLockProvider redisLockMock,
        IRedisIdempotencyProvider redisIdempotencyMock,
        IKafkaProducerService kafka) CreateServiceProviderWithMocks(IOptions<SimulationConfig>? simulationConfig = null)
    {
        // create DI-container and InMemory-db
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var redisLockMock = Substitute.For<IRedisLockProvider>();
        // locks can always be captured
        redisLockMock.TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns(new RedisLockToken("mock-key", "mock-token"));
        redisLockMock.IsLockedAsync(Arg.Any<string>()).Returns(false);
        
        var redisIdempotencyMock = Substitute.For<IRedisIdempotencyProvider>();
        // always no existing operation for idempotency key
        redisIdempotencyMock.TrySetIdempotencyKeyAsync(Arg.Any<string>()).Returns(true);
        redisIdempotencyMock.GetIdempotencyResultAsync<StartDeliveryResult>(Arg.Any<string>()).Returns((StartDeliveryResult?)null);

        var kafka = Substitute.For<IKafkaProducerService>();

        services.AddSingleton(redisLockMock);
        services.AddSingleton(redisIdempotencyMock);
        services.AddSingleton(kafka);
        if (simulationConfig != null)
            services.AddSingleton(simulationConfig);
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var manager = new ReservationManager(scopeFactory, redisLockMock, redisIdempotencyMock);
        
        return (serviceProvider, scopeFactory, redisLockMock, redisIdempotencyMock, kafka);
    }

    public static async Task<(string stationId, string tankId, string pumpId, string nozzleId)> SeedDefaultDataToDbAsync(ServiceProvider serviceProvider)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    
        using var arrangeScope = scopeFactory.CreateScope();
        var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
    
        // add test station, tank, pump and nozzle
        const string stationId = "station-1";
        const string tankId = "tank-1";
        const string pumpId = "pump-1";
            
        var station = new StationEntity { Id = stationId, Name = "Test", Address = "Somewhere" };
        var tank = new TankEntity
        {
            Id = tankId,
            FuelType = FuelType.Ai95,
            Capacity = 100,
            CurrentVolume = 100,
            StationId = station.Id
        };
        var pump = new PumpEntity { Id = pumpId, StationId = station.Id, Station = station};
        var nozzle = new NozzleEntity
        {
            Id = Guid.NewGuid().ToString(),
            FuelType = FuelType.Ai95,
            TankId = tank.Id,
            PumpId = pump.Id,
            Tank = tank,
            Pump = pump
        };
        station.Tanks.Add(tank);
        station.Pumps.Add(pump);
        pump.Nozzles.Add(nozzle);
        db.Stations.Add(station);
        db.Tanks.Add(tank);
        db.Pumps.Add(pump);
        db.Nozzles.Add(nozzle);
        await db.SaveChangesAsync();
    
        return (station.Id, tank.Id, pump.Id, nozzle.Id);
    }
    
    public static IOptions<SimulationConfig> CreateTestSimulationConfig(
        int minDeliveryMin = 0, int maxDeliveryMin = 0,
        int minUnloadMin = 0, int maxUnloadMin = 0,
        int speedFactor = 1,
        int maxTankFillRetries = 2, int tankFillRetryDelayMs = 1)
    {
        var config = new SimulationConfig
        {
            MinDeliveryDurationMinutes = minDeliveryMin,
            MaxDeliveryDurationMinutes = maxDeliveryMin,
            MinUnloadDurationMinutes = minUnloadMin,
            MaxUnloadDurationMinutes = maxUnloadMin,
            SpeedFactor = speedFactor,
            MaxTankFillRetriesCount = maxTankFillRetries,
            TankFillRetryDelayMs = tankFillRetryDelayMs
        };
        return Options.Create(config);
    }
}