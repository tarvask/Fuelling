namespace FuelStation.ReservationService.Models;

public class ErrorInfo
{
    public int NumericCode { get; }
    public string Code { get; }
    private string Template { get; }

    public ErrorInfo(int numericCode, string code, string template)
    {
        NumericCode = numericCode;
        Code = code;
        Template = template;
    }

    public string Format(params object[] args) => string.Format(Template, args);
}