using System.Globalization;
using FuelStation.ReservationService.Constants;
using StackExchange.Redis;

namespace FuelStation.ReservationService.Infrastructure;

public interface IRedisLockProvider
{
    Task<RedisLockToken?> TryAcquireLockAsync(string key, TimeSpan expiry);
    Task ReleaseLockAsync(RedisLockToken lockToken);
    Task SetTankVolumeAsync(string tankId, decimal volume);
    Task<bool> IsLockedAsync(string key);
}

public class RedisLockProvider : IRedisLockProvider
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
        await _db.StringSetAsync(LockConstants.TankVolumeCacheKey(tankId), volume.ToString(CultureInfo.InvariantCulture));
    }
    
    public async Task<bool> IsLockedAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }
}

public record RedisLockToken(string Key, string Token);