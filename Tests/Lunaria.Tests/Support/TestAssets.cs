using Lunaria.Game.Resources;
using System.Text.Json.Nodes;

namespace Lunaria.Tests.Support;

public sealed partial class TestAssets : IDisposable
{
    public const uint CharacterId = 1001;
    public const uint UnknownCharacterId = 4004;
    public const uint PricedGroup = 11;
    public const uint LockedGroup = 15;
    public const uint MaterialItem = 900;
    public const uint BreakItem = 901;
    public const uint SmallItem = 902;
    public const uint SpareItem = 903;
    public const uint UnknownItem = 999;
    public const uint CurrencyItem = 1;
    public const int CoinMoneyType = 1;
    public const int UnknownMoneyType = 7;
    public const ulong StarterMap = 100;
    public const ulong SecondMap = 200;
    public const ulong ClientOnlyMap = 300;
    public const ulong UnplayableMap = 400;
    public const ulong DefaultSavepoint = 8;
    public const ulong OtherSavepoint = 10;
    public const ulong Teleport = 500;
    public const uint Guide = 20001;
    public const uint SecondGuide = 20002;
    public const uint UnknownGuide = 39999;

    public const uint CapAtBreakZero = 3;

    public const uint CapAtBreakOne = 5;

    public const uint BreakWorldLevel = 2;

    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public TestAssets()
    {
        var tables = Directory.CreateDirectory(Path.Combine(_root, "tables"));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "assets", "tables")))
            directory = directory.Parent;

        var bundled = Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Bundled tables not found"), "assets",
            "tables");
        var policy = JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "assets", "gameplay-policy.json")))!;
        foreach (var (_, rule) in policy["wanted"]!["awards"]!.AsObject())
            if (rule?["currency"] is not null) rule["currency"] = nameof(MoneyType.Coins);
        File.WriteAllText(Path.Combine(_root, "gameplay-policy.json"), policy.ToJsonString());

        foreach (var file in Directory.EnumerateFiles(bundled, "*.json"))
        {
            File.Copy(file, Path.Combine(tables.FullName, Path.GetFileName(file)));
        }
        WriteTables(tables);
        WriteSystemTables(tables);

        Data = new GameData(_root);

        Data.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public GameData Data { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Use mergeOverBundled when other tables still reference bundled rows.</summary>
    private static void Write(DirectoryInfo dir, string file, string json, bool mergeOverBundled = false)
    {
        var path = Path.Combine(dir.FullName, file);

        if (!mergeOverBundled || !File.Exists(path))
        {
            File.WriteAllText(path, json);
            return;
        }

        var merged = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        foreach (var (rootKey, rootValue) in JsonNode.Parse(json)!.AsObject())
        {
            if (rootValue is not JsonObject rows)
            {
                merged[rootKey] = rootValue?.DeepClone();
                continue;
            }

            var target = merged[rootKey]?.AsObject()
                         ?? throw new InvalidOperationException($"{file} does not shape like the mini overlay");

            foreach (var (rowKey, row) in rows)
            {
                target[rowKey] = row?.DeepClone();
            }
        }

        File.WriteAllText(path, merged.ToJsonString());
    }
}
