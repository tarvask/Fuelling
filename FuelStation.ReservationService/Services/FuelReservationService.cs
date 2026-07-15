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

    public override Task<StartFuellingResponse> StartFuelling(StartFuellingRequest request, ServerCallContext context)
    {
        var result = _manager.StartFuelling(request.PumpId, request.FuelType, request.PreauthorizedLitres);

        if (result.Success)
            _ = _kafka.SendFuellingStartedEvent(result.SessionId!, request.PumpId, request.FuelType.ToString(), result.ReservedLitres);

        return Task.FromResult(new StartFuellingResponse
        {
            Success = result.Success,
            SessionId = result.SessionId ?? string.Empty,
            ReservedLitres = result.ReservedLitres,
            Error = result.Error ?? string.Empty
        });
    }

    public override Task<CompleteFuellingResponse> CompleteFuelling(CompleteFuellingRequest request, ServerCallContext context)
    {
        var result = _manager.CompleteFuelling(request.SessionId, request.ActualLitres);

        if (result.Success)
            _ = _kafka.SendFuellingCompletedEvent(request.SessionId, request.FuelType.ToString(), request.ActualLitres);

        return Task.FromResult(new CompleteFuellingResponse { Success = result.Success, Error = result.Error ?? "" });
    }

    public override Task<AddFuelFastResponse> AddFuelFast(AddFuelFastRequest request, ServerCallContext context)
    {
        var result = _manager.AddFuelFast(Enum.Parse<FuelType>(request.FuelType), request.Litres);

        if (result.Success)
            _ = _kafka.SendFuelAddedFastEvent(result.TankId!, request.FuelType, request.Litres, result.NewVolume);

        return Task.FromResult(new AddFuelFastResponse
        {
            Success = result.Success,
            TankId = result.TankId ?? string.Empty,
            NewVolume = result.NewVolume,
            Error = result.Error ?? string.Empty
        });
    }

    public override Task<StartDeliveryResponse> StartDelivery(StartDeliveryRequest request, ServerCallContext context)
    {
        var compartments = request.Compartments.ToList();
        var result = _manager.StartDelivery(compartments);

        if (result.Success)
            _ = _kafka.SendDeliveryStartedEvent(result.SessionId!, compartments);

        return Task.FromResult(new StartDeliveryResponse
        {
            Success = result.Success,
            SessionId = result.SessionId,
            Error = result.Error ?? string.Empty
        });
    }

    public override Task<CompleteDeliveryResponse> CompleteDelivery(CompleteDeliveryRequest request, ServerCallContext context)
    {
        var result = _manager.StopDelivery(request.SessionId);

        if (result.Success)
            _ = _kafka.SendDeliveryCompletedEvent(request.SessionId);

        return Task.FromResult(new CompleteDeliveryResponse
        {
            Success = result.Success,
            Error = result.Error ?? string.Empty
        });
    }
}