namespace Lunaria.Silver;

public sealed class SeqAllocator
{
    public SeqAllocator(uint start = 0)
    {
        Peek = start;
    }

    public uint Peek { get; private set; }

    public uint Allocate()
    {
        var current = Peek;
        Peek = unchecked(Peek + 1);
        return current;
    }

    public void Reset(uint next) => Peek = next;
}
