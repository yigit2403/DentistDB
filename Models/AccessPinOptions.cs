namespace DentistDB.Models;

public sealed class AccessPinOptions
{
    public const string SectionName = "AccessPins";
    public const string AdminPlaceholder = "CHANGE_ME_ADMIN_PIN";
    public const string WorkerPlaceholder = "CHANGE_ME_WORKER_PIN";

    private static readonly string[] WeakPins = { "1234", "5678", "0000", "1111", "123456", "12345678" };

    public string Admin { get; set; } = AdminPlaceholder;
    public string Worker { get; set; } = WorkerPlaceholder;

    public string? GetPinFor(AppAccountRole role) => role switch
    {
        AppAccountRole.Admin => Admin,
        AppAccountRole.Worker => Worker,
        _ => null
    };

    public static bool IsSecurePin(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, AdminPlaceholder, StringComparison.Ordinal)
            && !string.Equals(value, WorkerPlaceholder, StringComparison.Ordinal)
            && !WeakPins.Contains(value, StringComparer.Ordinal);
    }
}
