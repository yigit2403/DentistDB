namespace DentistDB.Services;

/// <summary>Validates Turkish national identity numbers (T.C. Kimlik No) using the official checksum.</summary>
public static class TcknValidator
{
    public static bool IsValid(string? tckn)
    {
        if (string.IsNullOrWhiteSpace(tckn) || tckn.Length != 11 || tckn[0] == '0')
        {
            return false;
        }

        var digits = new int[11];
        for (var i = 0; i < 11; i++)
        {
            if (!char.IsAsciiDigit(tckn[i]))
            {
                return false;
            }

            digits[i] = tckn[i] - '0';
        }

        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];

        var tenth = ((oddSum * 7) - evenSum) % 10;
        if (tenth < 0) tenth += 10;
        if (tenth != digits[9])
        {
            return false;
        }

        var eleventh = digits.Take(10).Sum() % 10;
        return eleventh == digits[10];
    }
}
