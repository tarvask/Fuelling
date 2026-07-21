using Microsoft.Extensions.Configuration;

namespace FuelStation.PaymentService.Infrastructure;

public class GrpcConfigProvider
{
    public GrpcConfigProvider()
    {
        var basePath = AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        GrpcAddress =
            Environment.GetEnvironmentVariable("Grpc__GrpcAddress")
            ?? configuration.GetValue<string>("Grpc:GrpcAddress")
            ?? "http://localhost:5001";
    }

    public string GrpcAddress { get; }
}