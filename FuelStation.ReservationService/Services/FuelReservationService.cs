using Fuel;
using Grpc.Core;

namespace FuelStation.ReservationService.Services;

public class FuelReservationService : FuelReservation.FuelReservationBase
{
    private readonly ReservationManager _reservationManager;
    private readonly DeliveryOrchestrator _deliveryOrchestrator;
    private readonly KafkaProducerService _kafka;

    public FuelReservationService(ReservationManager reservationManager, DeliveryOrchestrator deliveryOrchestrator, KafkaProducerService kafka)
    {
        _reservationManager = reservationManager;
        _deliveryOrchestrator = deliveryOrchestrator;
        _kafka = kafka;
    }

    public override async Task<StartFuellingResponse> StartFuelling(StartFuellingRequest request, ServerCallContext context)
    {
        var result = await _reservationManager.StartFuellingAsync(request.StationId, request.PumpId, request.FuelType, request.PreauthorizedLitres, request.IdempotencyKey);

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
        var result = await _reservationManager.CompleteFuellingAsync(request.StationId, request.SessionId, request.ActualLitres);

        if (result.Success)
            _ = _kafka.SendFuellingCompletedEvent(request.StationId, request.SessionId, request.FuelType.ToString(), request.ActualLitres);

        return new CompleteFuellingResponse { Success = result.Success, Error = result.Error ?? "" };
    }

    public override async Task<StartDeliveryResponse> StartDelivery(StartDeliveryRequest request, ServerCallContext context)
    {
        var result = await _deliveryOrchestrator.StartDeliveryProcessAsync(request.StationId, request.Compartments.ToList(), request.IdempotencyKey);

        return new StartDeliveryResponse
        {
            Success = result.Success,
            SessionId = result.SessionId,
            Error = result.Error ?? string.Empty
        };
    }

    public override async Task<GetStationsResponse> GetStations(GetStationsRequest request, ServerCallContext context)
    {
        var stations = await _reservationManager.GetStationsAsync();
        var response = new GetStationsResponse();
        response.Stations.AddRange(stations);
        return response;
    }
}