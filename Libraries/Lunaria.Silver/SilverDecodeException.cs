namespace Lunaria.Silver;


public sealed class SilverDecodeException : Exception
{
    public SilverDecodeException(string detail)
        : base($"SilverNet decode failure: {detail}")
    {
        Detail = detail;
    }

    public string Detail { get; }
}
