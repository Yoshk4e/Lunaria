using Lunaria.Common.Tracking;
namespace Lunaria.Common;


public sealed partial class GuidManager : TrackedObject
{
    public ulong Peek => checked(LastMinted + 1);

    private ulong __trackedLastMinted = default!;
    [Tracked]
    public partial ulong LastMinted { get; private set; }

    public ulong Next()
    {
        checked
        {
            LastMinted += 1;
        }
        return LastMinted;
    }

    public void Adopt(ulong id)
    {
        if (id > LastMinted)
            LastMinted = id;
    }

    public ulong NextMany(ulong count)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(count, other: 0UL);
        var last = checked(LastMinted + count);
        var first = LastMinted + 1;
        LastMinted = last;
        return first;
    }
}
