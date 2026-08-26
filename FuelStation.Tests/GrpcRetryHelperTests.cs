using FuelStation.Shared.Utilities;
using Grpc.Core;

namespace FuelStation.Tests;

public class GrpcRetryHelperTests
{
    [Fact]
    public async Task RetryGrpcCallAsync_SuccessFirstTry_ReturnsResult()
    {
        //# Arrange
        var expected = "ok";
    
        //# Act
        var result = await GrpcRetryHelper.RetryGrpcCallAsync(
            () => Task.FromResult(expected),
            maxRetries: 3,
            retryDelaySeconds: 1);
    
        //# Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public async Task RetryGrpcCallAsync_UnavailableTwice_ThenSucceeds()
    {
        //# Arrange
        int attempt = 0;
    
        //# Act
        const int retriesCount = 3;
        const string resultValue = "finally";
        var result = await GrpcRetryHelper.RetryGrpcCallAsync(
            () =>
            {
                attempt++;
                if (attempt < retriesCount)
                    throw new RpcException(new Status(StatusCode.Unavailable, "temporary"));
                return Task.FromResult(resultValue);
            },
            maxRetries: retriesCount,
            retryDelaySeconds: 1);
    
        //# Assert
        Assert.Equal(resultValue, result);
        Assert.Equal(retriesCount, attempt);
    }
    
    [Fact]
    public async Task RetryGrpcCallAsync_BusinessError_StopsImmediately()
    {
        //# Arrange
        int attempt = 0;
    
        //# Act & Assert
        await Assert.ThrowsAsync<RpcException>(() =>
            GrpcRetryHelper.RetryGrpcCallAsync<string>(
                () =>
                {
                    attempt++;
                    throw new RpcException(new Status(StatusCode.Internal, "bad request"));
                },
                maxRetries: 3,
                retryDelaySeconds: 1)
        );
    
        Assert.Equal(1, attempt);
    }
    
    [Fact]
    public async Task RetryGrpcCallAsync_AlwaysUnavailable_ThrowsLastException()
    {
        // #Arrange
        int attempt = 0;
    
        // #Act & Assert
        const int retriesCount = 3;
        await Assert.ThrowsAsync<RpcException>(() =>
            GrpcRetryHelper.RetryGrpcCallAsync<string>(
                () =>
                {
                    attempt++;
                    throw new RpcException(new Status(StatusCode.Unavailable, $"fail {attempt}"));
                },
                maxRetries: retriesCount,
                retryDelaySeconds: 1)
        );
    
        Assert.Equal(retriesCount, attempt);
    }
}