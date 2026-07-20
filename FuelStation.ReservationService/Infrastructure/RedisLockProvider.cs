using System.Globalization;
using StackExchange.Redis;

namespace FuelStation.ReservationService.Infrastructure;

public class RedisLockProvider
{
    private readonly IDatabase _db;

    public RedisLockProvider(IConnectionMultiplexer multiplexer)
    {
        _db = multiplexer.GetDatabase();
    }

    public async Task<RedisLockToken?> TryAcquireLockAsync(string key, TimeSpan expiry)
    {
        var token = Guid.NewGuid().ToString();
        bool acquired = await _db.StringSetAsync(key, token, expiry, When.NotExists);
        return acquired ? new RedisLockToken(key, token) : null;
    }

    public async Task ReleaseLockAsync(RedisLockToken lockToken)
    {
        var script = @"
            if redis.call('GET', KEYS[1]) == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            else
                return 0
            end";
        await _db.ScriptEvaluateAsync(script, new RedisKey[] { lockToken.Key }, new RedisValue[] { lockToken.Token });
    }
    
    public async Task SetTankVolumeAsync(string tankId, decimal volume)
    {
        await _db.StringSetAsync(RedisConstants.TankVolumeCacheKey(tankId), volume.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<decimal?> GetTankVolumeAsync(string tankId)
    {
        var value = await _db.StringGetAsync(RedisConstants.TankVolumeCacheKey(tankId));
        if (value.HasValue && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var volume))
            return volume;
        return null;
    }
}

public record RedisLockToken(string Key, string Token);