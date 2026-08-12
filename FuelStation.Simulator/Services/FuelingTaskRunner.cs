using Fuel;
using FuelStation.Shared.Utilities;
using FuelStation.Simulator.Infrastructure;
using FuelStation.Simulator.Models;

namespace FuelStation.Simulator.Services;

public class FuelingTaskRunner
{
    private readonly string _stationId;
    private readonly SimulationConfig _simulationConfig;
    private readonly SimulationConfigProvider _simulationConfigProvider;

    public FuelingTaskRunner(string stationId, SimulationConfig simulationConfig, SimulationConfigProvider simulationConfigProvider)
    {
        _stationId = stationId;
        _simulationConfig = simulationConfig;
        _simulationConfigProvider = simulationConfigProvider;
    }

    public async Task RunRandomFuellingAsync(FuelReservation.FuelReservationClient fuelReservationClient)
    {
        var fuelRequest = CreateRandomFuelData(_simulationConfig.FuelProbabilities, _stationId, _simulationConfig.MinLitres, _simulationConfig.MaxLitres);
        await FuelSingleCarSafeAsync(fuelReservationClient, fuelRequest);
    }
    
    private async Task FuelSingleCarSafeAsync(FuelReservation.FuelReservationClient fuelReservationClient, FuelRequest fuelRequest)
    {
        try
        {
            await FuelSingleCar(fuelReservationClient, fuelRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    private static FuelRequest CreateRandomFuelData(
        Dictionary<FuelType, double> probabilities, string stationId, int minLitres, int maxLitres)
    {
        double dice = Random.Shared.NextDouble();
        double cumulative = 0;
        FuelType selectedFuel = probabilities.Keys.First();
        foreach (var kvp in probabilities)
        {
            cumulative += kvp.Value;
            if (dice <= cumulative) { selectedFuel = kvp.Key; break; }
        }

        double litres = Random.Shared.Next(minLitres, maxLitres + 1);
        return new FuelRequest(stationId, selectedFuel, litres);
    }
    
    private async Task FuelSingleCar(FuelReservation.FuelReservationClient fuelReservationClient, FuelRequest fuelRequest)
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var startFuellingRequest = new StartFuellingRequest
        {
            StationId = fuelRequest.StationId,
            PumpId = string.Empty,
            FuelType = fuelRequest.FuelType,
            PreauthorizedLitres = fuelRequest.Litres,
            IdempotencyKey = idempotencyKey
        };
        StartFuellingResponse? startReply = await GrpcRetryHelper.RetryGrpcCallAsync<StartFuellingResponse>(() =>
            fuelReservationClient.StartFuellingAsync(startFuellingRequest).ResponseAsync);

        if (startReply == null || startReply.Success == false)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Start failed: {startReply?.Error ?? "No server answer"}");
            return;
        }

        Console.WriteLine($"[{DateTime.Now:T}] Session {startReply.SessionId}: reserved {startReply.ReservedLitres}L {fuelRequest.FuelType}");

        // Simulate fueling time
        await Task.Delay(GetTotalFuelingProcessDurationMs(fuelRequest.Litres));

        var completeFuellingRequest = new CompleteFuellingRequest
        {
            StationId = fuelRequest.StationId,
            SessionId = startReply.SessionId,
            FuelType = fuelRequest.FuelType,
            ActualLitres = startReply.ReservedLitres
        };
        CompleteFuellingResponse? completeReply = await GrpcRetryHelper.RetryGrpcCallAsync<CompleteFuellingResponse>(() =>
            fuelReservationClient.CompleteFuellingAsync(completeFuellingRequest).ResponseAsync);

        if (completeReply == null || completeReply.Success == false)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Complete of session {startReply.SessionId} failed: {completeReply?.Error ?? "No server answer"}");
            return;
        }

        Console.WriteLine(completeReply.Success
            ? $"[{DateTime.Now:T}] Session {startReply.SessionId} ended: actual {startReply.ReservedLitres}L, success={completeReply.Success}"
            : $"[{DateTime.Now:T}] Session {startReply.SessionId} ended: actual {startReply.ReservedLitres}L, success={completeReply.Success}, error={completeReply.Error}");
    }

    // timing helper functions
    int GetTotalFuelingProcessDurationMs(double litres)
    {
        return GetFuelingDurationFromVolumeMs(litres) + GetHumanFactorDurationMs();
    }

    int GetHumanFactorDurationMs()
    {
        int virtualMinutes = Random.Shared.Next(_simulationConfig.MinHumanFactorMinutes, _simulationConfig.MaxHumanFactorMinutes + 1);
        return virtualMinutes * 60 * 1000 / _simulationConfigProvider.SpeedFactor;
    }

    int GetFuelingDurationFromVolumeMs(double litres)
    {
        double pumpSpeed = _simulationConfig.PumpSpeedLitresPerMinute;
        int virtualMinutes = (int)Math.Ceiling(litres / pumpSpeed);
        return virtualMinutes * 60 * 1000 / _simulationConfigProvider.SpeedFactor;
    }

    private record FuelRequest(string StationId, FuelType FuelType, double Litres);
}