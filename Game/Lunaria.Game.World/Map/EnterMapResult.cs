namespace Lunaria.Game.World;

public readonly record struct EnterMapResult(int Code, ulong MapId)
{
    public bool Ok => Code == 0;
}
