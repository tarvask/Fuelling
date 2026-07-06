using Fuel;
using Grpc.Core;

namespace FuelStation.ReservationService.Services;

public class FuelReservationService : FuelReservation.FuelReservationBase
{
    private readonly ReservationManager _manager;
    private readonly KafkaProducerService _kafka;

    public FuelReservationService(ReservationManager manager, KafkaProducerService kafka)
    {
        _manager = manager;
        _kafka = kafka;
    }

    public override Task<StartResponse> StartFueling(StartRequest request, ServerCallContext context)
    {
        var result = _manager.StartFueling(request.PumpId, request.FuelType, request.PreauthorizedLitres);

        if (result.Success)
            _ = _kafka.SendStartedEvent(result.SessionId!, request.PumpId, request.FuelType.ToString(), result.ReservedLitres);

        return Task.FromResult(new StartResponse
        {
            Success = result.Success,
            SessionId = result.SessionId ?? "",
            ReservedLitres = result.ReservedLitres,
            Error = result.Error ?? ""
        });
    }

    public override Task<StopResponse> StopFueling(StopRequest request, ServerCallContext context)
    {
        var result = _manager.StopFueling(request.SessionId, request.ActualLitres);

        if (result.Success)
            _ = _kafka.SendCompletedEvent(request.SessionId, request.FuelType.ToString(), request.ActualLitres);

        return Task.FromResult(new StopResponse { Success = result.Success, Error = result.Error ?? "" });
    }

    public override Task<AddFuelResponse> AddFuel(AddFuelRequest request, ServerCallContext context)
    {
        var result = _manager.AddFuel(Enum.Parse<FuelType>(request.FuelType), request.Litres);

        if (result.Success)
            _ = _kafka.SendFuelAddedEvent(result.TankId!, request.FuelType, request.Litres, result.NewVolume);

        return Task.FromResult(new AddFuelResponse
        {
            Success = result.Success,
            TankId = result.TankId ?? "",
            NewVolume = result.NewVolume,
            Error = result.Error ?? ""
        });
    }
}