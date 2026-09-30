namespace Lunaria.Game.Resources;

internal static class RewardStrings
{
    public static IReadOnlyList<ItemGrant> Parse(uint sourceId, IEnumerable<string> values)
    {
        var result = new List<ItemGrant>();

        foreach (var value in values)
        {
            var parts = value.Split(separator: '=', count: 2);

            if (parts.Length != 2
                || !uint.TryParse(parts[0], out var itemId)
                || !uint.TryParse(parts[1], out var count)
                || itemId == 0
                || count == 0)
                throw new ResourceException("Reward strings", $"reward row {sourceId} has invalid item '{value}'");

            result.Add(new ItemGrant(itemId, count));
        }

        return result;
    }
}
