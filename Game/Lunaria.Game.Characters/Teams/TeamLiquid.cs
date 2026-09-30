using Msg;

namespace Lunaria.Game.Characters;

/// <summary>Wire values use basis points, where 10000 means 100%.</summary>
public sealed record TeamLiquid
{
    public const int MaxBasisPoints = 10_000;

    public static TeamLiquid Empty { get; } = new();

    public int Fire { get; init; }
    public int Ice { get; init; }
    public int Thunder { get; init; }
    public int Gravity { get; init; }
    public int Radiate { get; init; }
    public int Silver { get; init; }
    public int Blackiron { get; init; }

    public static TeamLiquid FromProto(ElementParamMap? map) => new() {
        Fire = Clamp(map?.Fire ?? 0),
        Ice = Clamp(map?.Ice ?? 0),
        Thunder = Clamp(map?.Thunder ?? 0),
        Gravity = Clamp(map?.Gravity ?? 0),
        Radiate = Clamp(map?.Radiate ?? 0),
        Silver = Clamp(map?.Silver ?? 0),
        Blackiron = Clamp(map?.Blackiron ?? 0)
    };

    public TeamLiquid Normalized() => new() {
        Fire = Clamp(Fire),
        Ice = Clamp(Ice),
        Thunder = Clamp(Thunder),
        Gravity = Clamp(Gravity),
        Radiate = Clamp(Radiate),
        Silver = Clamp(Silver),
        Blackiron = Clamp(Blackiron)
    };

    public TeamLiquid Add(int element, int amount) => element switch {
        1 => this with { Fire = Clamp((long)Fire + amount) },
        2 => this with { Ice = Clamp((long)Ice + amount) },
        3 => this with { Thunder = Clamp((long)Thunder + amount) },
        4 => this with { Gravity = Clamp((long)Gravity + amount) },
        5 => this with { Radiate = Clamp((long)Radiate + amount) },
        6 => this with { Silver = Clamp((long)Silver + amount) },
        7 => this with { Blackiron = Clamp((long)Blackiron + amount) },
        _ => this
    };

    public ElementParamMap ToProto() => new() {
        Fire = Fire,
        Ice = Ice,
        Thunder = Thunder,
        Gravity = Gravity,
        Radiate = Radiate,
        Silver = Silver,
        Blackiron = Blackiron
    };

    private static int Clamp(long value) => (int)Math.Clamp(value, min: 0, MaxBasisPoints);
}
