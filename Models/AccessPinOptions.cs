namespace DentistDB.Models;

public sealed class AccessPinOptions
{
    public const string SectionName = "AccessPins";

    public string Admin { get; set; } = "1234";
    public string Worker { get; set; } = "5678";

    public string? GetPinFor(string accountKey)
    {
        return accountKey.ToLowerInvariant() switch
        {
            "admin" => Admin,
            "worker" => Worker,
            _ => null
        };
    }
}
