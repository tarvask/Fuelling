using System;
using System.Collections.Generic;
using FuelStation.SupplyServiceUI.Models;
using Microsoft.Extensions.Configuration;

namespace FuelStation.SupplyServiceUI.Infrastructure;

public class AppConfigProvider
{
    public List<TankerConfig> Tankers { get; }
    public string GrpcAddress { get; }

    public AppConfigProvider()
    {
        var basePath = AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        GrpcAddress = configuration.GetValue<string>("GrpcAddress") ?? "http://localhost:5001";
        Tankers = configuration.GetSection("Tankers").Get<List<TankerConfig>>() ?? new List<TankerConfig>();
    }
}