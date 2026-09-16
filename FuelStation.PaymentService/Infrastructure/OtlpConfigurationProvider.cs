using Microsoft.Extensions.Configuration;

namespace FuelStation.PaymentService.Infrastructure;

public class OtlpConfigurationProvider
{
    public OtlpConfigurationProvider(IConfiguration configuration)
    {
        Endpoint = configuration.GetValue<string>("OtlpEndpoint")
                   ?? "http://localhost:4317";
    }

    public string Endpoint { get; }
}