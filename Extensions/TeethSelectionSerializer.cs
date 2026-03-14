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
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .ToArray();
    }

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
