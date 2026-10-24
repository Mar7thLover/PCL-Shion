using System.Globalization;
using System.Text.RegularExpressions;

namespace PCL.Shion.Controls;

internal static partial class SvgNumberParser
{
    [GeneratedRegex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex NumberRegex();

    public static double TryParse(string? value, double fallback = 0D)
    {
        return TryParseNullable(value) ?? fallback;
    }

    public static double? TryParseNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = NumberRegex().Match(value.Trim());
        if (!match.Success)
            return null;

        return double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    public static double[] ParseNumberList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var matches = NumberRegex().Matches(value);
        var result = new double[matches.Count];
        for (var i = 0; i < matches.Count; i++)
            result[i] = double.Parse(matches[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

        return result;
    }
}
