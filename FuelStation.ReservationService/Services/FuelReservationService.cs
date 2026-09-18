using Fuel;
using FuelStation.ReservationService.Models;
using FuelStation.Shared.Constants;
using Grpc.Core;

namespace FuelStation.ReservationService.Services;

public class FuelReservationService : FuelReservation.FuelReservationBase
{
    private readonly ReservationManager _reservationManager;
    private readonly DeliveryOrchestrator _deliveryOrchestrator;
    private readonly IKafkaProducerService _kafka;

    public FuelReservationService(ReservationManager reservationManager, DeliveryOrchestrator deliveryOrchestrator, IKafkaProducerService kafka)
    {
        _reservationManager = reservationManager;
        _deliveryOrchestrator = deliveryOrchestrator;
        _kafka = kafka;
    }

    public override async Task<StartFuellingResponse> StartFuelling(StartFuellingRequest request, ServerCallContext context)
    {
        var result = await _reservationManager.StartFuellingAsync(request.StationId, request.PumpId, request.FuelType, request.PreauthorizedLitres, request.IdempotencyKey);

        if (result.Success)
        {
            _ = _kafka.SendFuellingStartedEvent(request.StationId, result.SessionId!, request.PumpId,
                request.FuelType.ToString(), result.ReservedLitres);
            Metrics.FuelStationMetrics.FuellingStarted.Inc();
        }
        else
        {
            Metrics.FuelStationMetrics.Errors.WithLabels(
                request.StationId,
                OpenTelemetryConstants.Operations.StartFuelling,
                result.ErrorCode ?? ErrorCatalog.Unknown.Code).Inc();
        }

        return new StartFuellingResponse
        {
            Success = result.Success,
            SessionId = result.SessionId ?? string.Empty,
            ReservedLitres = result.ReservedLitres,
            ErrorNumericCode = result.ErrorNumericCode ?? 0,
            ErrorCode = result.ErrorCode ?? string.Empty,
            ErrorText = result.ErrorText ?? string.Empty
        };
    }

    public override async Task<CompleteFuellingResponse> CompleteFuelling(CompleteFuellingRequest request, ServerCallContext context)
    {
        var result = await _reservationManager.CompleteFuellingAsync(request.StationId, request.SessionId, request.ActualLitres);

        if (result.Success)
        {
            _ = _kafka.SendFuellingCompletedEvent(request.StationId, request.SessionId, request.FuelType.ToString(),
                request.ActualLitres);
            Metrics.FuelStationMetrics.FuellingCompleted.Inc();
        }
        else
        {
            Metrics.FuelStationMetrics.Errors.WithLabels(
                request.StationId,
                OpenTelemetryConstants.Operations.CompleteFuelling,
                result.ErrorCode ?? ErrorCatalog.Unknown.Code).Inc();
        }

        return new CompleteFuellingResponse
        {
            Success = result.Success,
            ErrorNumericCode = result.ErrorNumericCode ?? 0,
            ErrorCode = result.ErrorCode ?? string.Empty,
            ErrorText = result.ErrorText ?? string.Empty
        };
    }

    public override async Task<StartDeliveryResponse> StartDelivery(StartDeliveryRequest request, ServerCallContext context)
    {
        var result = await _deliveryOrchestrator.StartDeliveryProcessAsync(request.StationId, request.Compartments.ToList(), request.IdempotencyKey);

        if (result.Success)
        {
            Metrics.FuelStationMetrics.DeliveryStarted.Inc();
        }
        else
        {
            Metrics.FuelStationMetrics.Errors.WithLabels(
                request.StationId,
                OpenTelemetryConstants.Operations.StartDelivery,
                result.ErrorCode ?? ErrorCatalog.Unknown.Code).Inc();
        }

        return new StartDeliveryResponse
        {
            Success = result.Success,
            SessionId = result.SessionId,
            ErrorNumericCode = result.ErrorNumericCode ?? 0,
            ErrorCode = result.ErrorCode ?? string.Empty,
            ErrorText = result.ErrorText ?? string.Empty
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