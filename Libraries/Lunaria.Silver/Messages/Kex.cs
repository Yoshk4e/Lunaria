namespace Lunaria.Silver;

internal static class Kex
{
    public const int PayloadLength = 75;
    public const int Blob1Offset = 5;
    public const int Blob2Offset = 21;
    public const int MarkerOffset = 45;
    public const int PublicKeyOffset = 47;
    public const int MinLength = PublicKeyOffset + 16;
}
