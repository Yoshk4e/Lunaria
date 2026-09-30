using Google.Protobuf;
using Lunaria.Game.Resources.Tables;
using Msg;

namespace Lunaria.Game.Resources;

public sealed class GlobalConfigAssets
{
    private readonly Dictionary<string, float> _values = [];

    public GlobalConfigAssets(IReadOnlyDictionary<string, SServerGlobalConfig> rows)
    {
        foreach (var row in rows.Values)
        {
            if (float.TryParse(row.Value.Trim(), out var value))
                _values[row.Key] = value;
        }
    }

    public int SatietyLimit => (int)Value("SatietyLimit");
    public int StaminaRegenMax => (int)Value("StaminaRegenMax");
    public int MaxStamina => (int)Value("MaxStamina");

    /// <summary>Seconds per regenerated stamina point.</summary>
    public int StaminaRegenInterval => (int)Value("StaminaRegenInterval");

    public int MaxSilverCreatureCost => (int)Value("MaxSilverCreatureCost");
    public int MaxSilverCreatureNum => (int)Value("MaxSilverCreatureNum");

    /// <summary>Centimetres.</summary>
    public int UnlockCollectionRange => (int)Value("UnlockCollectionRange");

    public int MaxBagCell => (int)Value("MaxBagCell");

    public int MailMaxSaveCount => (int)Value("MailMaxSaveCount");

    public uint BagFullMailId => (uint)Value("BagFullMailID");

    public uint DefaultCharacter => (uint)Value("DefaultCharacter");

    public GlobalConf GlobalConf => new() {
        SatietyLimit = SatietyLimit,
        StaminaRegenMax = StaminaRegenMax,
        MaxStamina = MaxStamina,
        StaminaRegenInterval = StaminaRegenInterval,
        MaxSilverCreatureCost = MaxSilverCreatureCost,
        MaxSilverCreatureNum = MaxSilverCreatureNum,
        UnlockCollectionRange = UnlockCollectionRange,
        // The date is an Excel serial number. Leave the byte field empty until its client format is known.
        LaunchDate = ByteString.Empty,
        MaxBagCell = MaxBagCell
    };

    private float Value(string key) =>
        _values.TryGetValue(key, out var value) ?
            value :
            throw new ResourceException("S_ServerGlobalConfig.json", $"s_serverglobalconfig: missing key {key}");
}
