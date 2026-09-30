namespace Lunaria.Game.Motives;

public readonly record struct MotiveGrant(int Code, ulong UniqId)
{
    public bool Ok => Code == 0;
    public static MotiveGrant Rejected(int code) => new(code, UniqId: 0);
}
