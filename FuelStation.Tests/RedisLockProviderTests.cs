using FuelStation.ReservationService.Infrastructure;
using NSubstitute;
using StackExchange.Redis;

namespace FuelStation.Tests;

public class RedisLockProviderTests
{
    [Fact]
    public async Task TryAcquireLockAsync_FreeKey_ReturnsToken()
    {
        //# Arrange
        // base
        var (db, provider) = CreateDbAndLockProvider();

        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan>(), Arg.Any<When>())
            .Returns(true);

        //# Act
        const string lockedTokenId = "lock:test";
        var token = await provider.TryAcquireLockAsync(lockedTokenId, TimeSpan.FromSeconds(10));

        //# Assert
        Assert.NotNull(token);
        Assert.Equal(lockedTokenId, token.Key);
        Assert.False(string.IsNullOrEmpty(token.Token));
    }
    
    [Fact]
    public async Task TryAcquireLockAsync_BusyKey_ReturnsNull()
    {
        //# Arrange
        // base
        var (db, provider) = CreateDbAndLockProvider();

        const string lockedTokenId = "lock:busy";
        db.StringSetAsync(Arg.Is<RedisKey>(k => k == lockedTokenId),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<When>())
            .Returns(false);

        //# Act
        var token = await provider.TryAcquireLockAsync(lockedTokenId, TimeSpan.FromSeconds(10));

        //# Assert
        Assert.Null(token);
    }
    
    
    [Fact]
    public async Task ReleaseLockAsync_CallsScriptEvaluateWithCorrectKeyAndToken()
    {
        //# Arrange
        // base
        var (db, provider) = CreateDbAndLockProvider();

        // prepare "released" token
        const string tokenKey = "lock:test";
        const string tokenValue = "token123";
        var token = new RedisLockToken(tokenKey, tokenValue);

        // Act
        await provider.ReleaseLockAsync(token);

        //# Assert
        await db.Received(1).ScriptEvaluateAsync(
            Arg.Is<string>(script => script.Contains("GET") && script.Contains("DEL")),
            Arg.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0] == tokenKey),
            Arg.Is<RedisValue[]>(values =>
                values.Length == 1 &&
                values[0] == tokenValue)
        );
    }
    
    [Fact]
    public async Task IsLockedAsync_ExistingKey_ReturnsTrue()
    {
        //# Arrange
        // base
        var (db, provider) = CreateDbAndLockProvider();

        const string lockedTokenId = "lock:test";
        db.KeyExistsAsync(Arg.Is<RedisKey>(k => k == lockedTokenId),
                Arg.Any<CommandFlags>())
            .Returns(true);
        
        //# Act
        var isLocked = await provider.IsLockedAsync(lockedTokenId);

        //# Assert
        Assert.True(isLocked);
    }

    private (IDatabase, RedisLockProvider) CreateDbAndLockProvider()
    {
        var db = Substitute.For<IDatabase>();
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        return (db, new RedisLockProvider(multiplexer));
    }
}