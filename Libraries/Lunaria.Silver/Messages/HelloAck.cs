namespace Lunaria.Silver;

public static class HelloAck
{
    public static byte[] Encode(byte status = 0, HelloAuthMode authMode = HelloAuthMode.Auth)
    {
        var p = new byte[10];
        p[0] = status;
        p[1] = (byte)authMode;
        return p;
    }
}
