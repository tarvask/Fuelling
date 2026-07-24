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
        var result = await _manager.StartFuellingAsync(request.StationId, request.PumpId, request.FuelType, request.PreauthorizedLitres);

        if (result.Success)
            _ = _kafka.SendFuellingStartedEvent(request.StationId, result.SessionId!, request.PumpId, request.FuelType.ToString(), result.ReservedLitres);

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
        var result = await _manager.CompleteFuellingAsync(request.StationId, request.SessionId, request.ActualLitres);

        if (result.Success)
            _ = _kafka.SendFuellingCompletedEvent(request.StationId, request.SessionId, request.FuelType.ToString(), request.ActualLitres);

        return new CompleteFuellingResponse { Success = result.Success, Error = result.Error ?? "" };
    }

    public override async Task<AddFuelFastResponse> AddFuelFast(AddFuelFastRequest request, ServerCallContext context)
    {
        var result = await _manager.AddFuelFastAsync(request.StationId, Enum.Parse<FuelType>(request.FuelType), request.Litres);

        if (result.Success)
            _ = _kafka.SendFuelAddedFastEvent(request.StationId, result.TankId!, request.FuelType, request.Litres, result.NewVolume);

        return new AddFuelFastResponse
        {
            Success = result.Success,
            TankId = result.TankId ?? string.Empty,
            NewVolume = result.NewVolume,
            Error = result.Error ?? string.Empty
        };
    }

    public override async Task<StartDeliveryResponse> StartDelivery(StartDeliveryRequest request, ServerCallContext context)
    {
        var compartments = request.Compartments.ToList();
        var result = await _manager.StartDeliveryAsync(request.StationId, compartments);

        if (result.Success)
            _ = _kafka.SendDeliveryStartedEvent(request.StationId, result.SessionId!, compartments);

        return new StartDeliveryResponse
        {
            Success = result.Success,
            SessionId = result.SessionId,
            Error = result.Error ?? string.Empty
        };
    }

    public override async Task<CompleteDeliveryResponse> CompleteDelivery(CompleteDeliveryRequest request, ServerCallContext context)
    {
        var result = await _manager.CompleteDeliveryAsync(request.StationId, request.SessionId);

        if (result.Success)
            _ = _kafka.SendDeliveryCompletedEvent(request.StationId, request.SessionId);

        return new CompleteDeliveryResponse
        {
            Success = result.Success,
            Error = result.Error ?? string.Empty
        };
    }
}