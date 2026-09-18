using System.Globalization;
using System.Text;
using DentistDB.Models;

namespace DentistDB.Services;

/// <summary>
/// Folds Turkish text to a lower-case ASCII-ish form so that SQLite LIKE (which is only
/// case-insensitive for ASCII) can match "Ömer" against "ömer", "omer" or "ÖMER".
/// </summary>
public static class SearchNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lowered = text.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        var builder = new StringBuilder(lowered.Length);

        foreach (var ch in lowered)
        {
            builder.Append(ch switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ı' => 'i',
                'i' => 'i',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                'â' => 'a',
                'î' => 'i',
                'û' => 'u',
                _ => ch
            });
        }

        var collapsed = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed;
    }

    public static string BuildPatientIndex(Patient patient)
    {
        var parts = new[]
        {
            Normalize(patient.FullName),
            Normalize(patient.Tckn),
            Normalize(DigitsOnly(patient.Phone)),
            Normalize(patient.Phone),
            Normalize(patient.Email)
        };

        var index = string.Join(' ', parts.Where(p => p.Length > 0));
        return index.Length > 400 ? index[..400] : index;
    }

    /// <summary>Searchable text of a treatment record: title, diagnosis, procedures, prescription, notes and teeth.</summary>
    public static string BuildOperationIndex(PreviousOperation operation)
    {
        var parts = new[]
        {
            Normalize(operation.Title),
            Normalize(operation.Diagnosis),
            Normalize(operation.Procedures),
            Normalize(operation.Prescriptions),
            Normalize(operation.Notes),
            Normalize(operation.SelectedTeethData?.Replace(',', ' '))
        };

        var index = string.Join(' ', parts.Where(p => p.Length > 0));
        return index.Length > 600 ? index[..600] : index;
    }

    public static string DigitsOnly(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return new string(text.Where(char.IsDigit).ToArray());
    }
}
