using System.Text.RegularExpressions;

namespace DentistDB.Extensions;

public static partial class TreatmentRecordTeethSerializer
{
    private const string Prefix = "[DISLER:";

    [GeneratedRegex(@"\[DISLER:(?<teeth>[0-9,\s]+)\]\s*", RegexOptions.Compiled)]
    private static partial Regex TeethPrefixRegex();

    public static IReadOnlyList<string> ParseSelectedTeeth(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return Array.Empty<string>();
        }

        var match = TeethPrefixRegex().Match(notes);
        if (!match.Success)
        {
            return Array.Empty<string>();
        }

        return match.Groups["teeth"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .ToArray();
    }

    public static string StripMetadata(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return string.Empty;
        }

        return TeethPrefixRegex().Replace(notes, string.Empty).Trim();
    }

    public static string Merge(string? notes, IEnumerable<string>? selectedTeeth)
    {
        var cleanedNotes = StripMetadata(notes);
        var teeth = (selectedTeeth ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value)
            .ToArray();

        if (teeth.Length == 0)
        {
            return cleanedNotes;
        }

        var prefix = $"{Prefix}{string.Join(",", teeth)}]";
        return string.IsNullOrWhiteSpace(cleanedNotes)
            ? prefix
            : $"{prefix} {cleanedNotes}";
    }
}
