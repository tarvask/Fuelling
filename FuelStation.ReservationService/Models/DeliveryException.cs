namespace FuelStation.ReservationService.Models;

public sealed class DeliveryException : Exception
{
    public ErrorInfo Error { get; }

    public DeliveryException(ErrorInfo error, params object[] args)
        : base(error.Format(args))
    {
        Error = error;
    }
}