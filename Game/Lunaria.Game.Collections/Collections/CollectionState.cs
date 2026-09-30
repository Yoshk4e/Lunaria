using Msg;

namespace Lunaria.Game.Collections;

public sealed record CollectionState(
    ulong Uniq,
    uint Cfg,
    EnmCollectionStatus Status,
    DateTimeOffset StatusTime,
    ulong Block,
    int X,
    int Y,
    int Z
);
