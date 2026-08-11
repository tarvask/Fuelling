using FuelStation.Shared.Constants;
using Grpc.Core;

namespace FuelStation.Shared.Utilities;

public static class GrpcRetryHelper
{
    public static async Task<T?> RetryGrpcCallAsync<T>(
        Func<Task<T>> grpcCall,
        int maxRetries = GrpcConstants.GrpcRequestRetryCount,
        int retryDelaySeconds = GrpcConstants.GrpcRequestRetryTimeout)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return await grpcCall();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                // the last try
                if (i == maxRetries - 1)
                    throw;
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }
        return default; // unreachable, but needed for compilation
    }
}