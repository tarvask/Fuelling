using System.Text.Json;
using FuelStation.ReservationService.Constants;
using StackExchange.Redis;

namespace FuelStation.ReservationService.Infrastructure;

public class RedisIdempotencyProvider
{
    private readonly IDatabase _db;
    private readonly TimeSpan _idempotencyTtl;

    public RedisIdempotencyProvider(IConnectionMultiplexer multiplexer)
    {
        _db = multiplexer.GetDatabase();
        _idempotencyTtl = TimeSpan.FromHours(IdempotencyConstants.IdempotencyKeyTtlHours);
    }

    public async Task<bool> TrySetIdempotencyKeyAsync(string key)
    {
        var wasSet = await _db.StringSetAsync(key, IdempotencyConstants.ProcessingStatus, _idempotencyTtl, When.NotExists);
        return wasSet;
    }
    
    public async Task SetIdempotencyResultAsync(string key, string result)
    {
        await _db.StringSetAsync(key, result, _idempotencyTtl);
    }

    public async Task<T?> GetIdempotencyResultAsync<T>(string key) where T : class
    {
        var json = await _db.StringGetAsync(key);
        if (json.IsNullOrEmpty || json == IdempotencyConstants.ProcessingStatus)
            return null;
        return JsonSerializer.Deserialize<T>(json!);
    }
    
    public async Task<T?> WaitForIdempotentResultAsync<T>(string idempotencyKey) where T : class
    {
        for (int i = 0; i < IdempotencyConstants.IdempotentResultRetryCount; i++)
        {
            await Task.Delay(IdempotencyConstants.IdempotentResultWaitDurationMs);
            var cached = await GetIdempotencyResultAsync<T>(idempotencyKey);
            if (cached != null)
                return cached;
        }
        
        return null;
    }
}