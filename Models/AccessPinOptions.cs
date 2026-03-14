namespace DentistDB.Models;

public sealed class AccessPinOptions
{
    public const string SectionName = "AccessPins";
    public const string AdminPlaceholder = "CHANGE_ME_ADMIN_PIN";
    public const string WorkerPlaceholder = "CHANGE_ME_WORKER_PIN";

    public string Admin { get; set; } = AdminPlaceholder;
    public string Worker { get; set; } = WorkerPlaceholder;

    public string? GetPinFor(string accountKey)
    {
        return accountKey.ToLowerInvariant() switch
        {
            "admin" => Admin,
            "worker" => Worker,
            _ => null
        };
    }

    public bool UsesSecurePins()
    {
        return IsConfiguredPin(Admin, AdminPlaceholder) && IsConfiguredPin(Worker, WorkerPlaceholder);
    }

    private static bool IsConfiguredPin(string? value, string placeholder)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, placeholder, StringComparison.Ordinal)
            && !string.Equals(value, "1234", StringComparison.Ordinal)
            && !string.Equals(value, "5678", StringComparison.Ordinal);
    }
}
