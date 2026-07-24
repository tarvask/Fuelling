using Fuel;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.ReservationService.Services;

public class DbInitializerService
{
    public async Task InitDbFromConfig(AppDbContext db, FuelNetworkConfig config)
    {
        if (await db.Stations.AnyAsync())
            return;

        foreach (var stationConfig in config.Stations)
        {
            // station
            var stationEntity = new StationEntity
            {
                Id = stationConfig.Id,
                Name = stationConfig.Name,
                Address = stationConfig.Address
            };
            db.Stations.AddRange(stationEntity);
            
            // tanks
            var tankEntities = stationConfig.Tanks.Select(t => new TankEntity
            {
                Id = t.Id,
                FuelType = Enum.Parse<FuelType>(t.FuelType, ignoreCase: true),
                Capacity = t.Capacity,
                CurrentVolume = t.CurrentVolume,
                StationId = stationConfig.Id
            });
            db.Tanks.AddRange(tankEntities);

            // pumps and nozzles
            foreach (var pumpConfig in stationConfig.Pumps)
            {
                var pumpEntity = new PumpEntity
                {
                    Id = pumpConfig.Id,
                    StationId = stationConfig.Id
                };
                db.Pumps.Add(pumpEntity);
            
                foreach (var nozzleConfig in pumpConfig.Nozzles)
                {
                    var nozzleEntity = new NozzleEntity
                    {
                        Id = Guid.NewGuid().ToString(),
                        FuelType = Enum.Parse<FuelType>(nozzleConfig.FuelType, ignoreCase: true),
                        TankId = nozzleConfig.TankId,
                        PumpId = pumpConfig.Id,
                    };
                    db.Nozzles.Add(nozzleEntity);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}