namespace DentistDB.Extensions;

public static class TeethSelectionSerializer
{
    public static IReadOnlyList<string> Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return Array.Empty<string>();
        }

        return data
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(IsFdiNumber) // drop typed garbage such as "abc" or "99"
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .ToArray();
    }

    /// <summary>Two digits, each 1–8: permanent quadrants 1–4 and deciduous 5–8.</summary>
    public static bool IsFdiNumber(string value) =>
        value.Length == 2 && value[0] is >= '1' and <= '8' && value[1] is >= '1' and <= '8';

    /// <summary>Cleans a comma-separated list typed or posted by the tooth selector ("17, 16,16" → "16,17").</summary>
    public static string? Normalize(string? csv) => Serialize(Parse(csv));

    public static string? Serialize(IEnumerable<string>? selectedTeeth)
    {
        var teeth = (selectedTeeth ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .ToArray();

        return teeth.Length == 0 ? null : string.Join(",", teeth);
    }
}
