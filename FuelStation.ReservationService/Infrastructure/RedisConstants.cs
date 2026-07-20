namespace FuelStation.ReservationService.Infrastructure;

public static class RedisConstants
{
    public const string StationDeliveryLockKey = "lock:station:delivery";
    public static string PumpLockKey(string pumpId) => $"lock:pump:{pumpId}";
    public static string TankLockKey(string tankId) => $"lock:tank:{tankId}";
    
    public static string TankVolumeCacheKey(string tankId) => $"tank:{tankId}:volume";

    public const int PumpLockExpireTime = 30;
    public const int TankLockExpireTime = 10;
    public const int StationLockExpireTime = 30;
}