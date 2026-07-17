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

    public override async Task<StartFuellingResponse> StartFuelling(StartFuellingRequest request, ServerCallContext context)
    {
        var result = await _manager.StartFuellingAsync(request.PumpId, request.FuelType, request.PreauthorizedLitres);

        if (result.Success)
            _ = _kafka.SendFuellingStartedEvent(result.SessionId!, request.PumpId, request.FuelType.ToString(), result.ReservedLitres);

        return new StartFuellingResponse
        {
            Success = result.Success,
            SessionId = result.SessionId ?? string.Empty,
            ReservedLitres = result.ReservedLitres,
            Error = result.Error ?? string.Empty
        };
    }

    public override async Task<CompleteFuellingResponse> CompleteFuelling(CompleteFuellingRequest request, ServerCallContext context)
    {
        var result = await _manager.CompleteFuellingAsync(request.SessionId, request.ActualLitres);

        if (result.Success)
            _ = _kafka.SendFuellingCompletedEvent(request.SessionId, request.FuelType.ToString(), request.ActualLitres);

        return new CompleteFuellingResponse { Success = result.Success, Error = result.Error ?? "" };
    }

    public override async Task<AddFuelFastResponse> AddFuelFast(AddFuelFastRequest request, ServerCallContext context)
    {
        var result = await _manager.AddFuelFastAsync(Enum.Parse<FuelType>(request.FuelType), request.Litres);

        if (result.Success)
            _ = _kafka.SendFuelAddedFastEvent(result.TankId!, request.FuelType, request.Litres, result.NewVolume);

        return new AddFuelFastResponse
        {
            Success = result.Success,
            TankId = result.TankId ?? string.Empty,
            NewVolume = result.NewVolume,
            Error = result.Error ?? string.Empty
        };
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

    public override async Task<CompleteDeliveryResponse> CompleteDelivery(CompleteDeliveryRequest request, ServerCallContext context)
    {
        var result = await _manager.CompleteDeliveryAsync(request.SessionId);

        if (result.Success)
            _ = _kafka.SendDeliveryCompletedEvent(request.SessionId);

        return new CompleteDeliveryResponse
        {
            Success = result.Success,
            Error = result.Error ?? string.Empty
        };
    }
}