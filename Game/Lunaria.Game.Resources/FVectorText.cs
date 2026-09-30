using System.Globalization;

namespace Lunaria.Game.Resources;

/// <summary>Read comma-separated UE vectors and round coordinates to whole centimetres for the wire.</summary>
internal static class FVectorText
{
    public static (int X, int Y, int Z) Parse(string blob, string file)
    {
        var parts = blob.Split(separator: ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 3)
            throw new ResourceException(file, $"malformed FVector blob: {blob}");

        var nums = new int[3];

        for (var i = 0; i < 3; i++)
        {
            var s = parts[i];

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                nums[i] = n;
                continue;
            }

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                nums[i] = (int)Math.Round(d, MidpointRounding.AwayFromZero);
                continue;
            }

            throw new ResourceException(file, $"malformed FVector blob: {blob}");
        }

        return (nums[0], nums[1], nums[2]);
    }
}
