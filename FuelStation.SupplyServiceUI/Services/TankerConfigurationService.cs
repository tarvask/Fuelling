using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuelStation.SupplyServiceUI.Models;

namespace FuelStation.SupplyServiceUI.Services;

public class TankerConfigurationService
{
    private readonly JsonSerializerOptions _jsonOptions;

    public TankerConfigurationService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public List<TankerConfig> LoadTankers()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<TankersConfiguration>(json, _jsonOptions);
        return config?.Tankers ?? new List<TankerConfig>();
    }
}