namespace DentistDB.Models;

public enum AppAccountRole
{
    Admin,
    Worker
}

public sealed class AppAccount
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required AppAccountRole Role { get; init; }
}

public static class AppAccounts
{
    public const string SessionKey = "CurrentAppAccount";

    public static readonly AppAccount Admin = new()
    {
        Key = "admin",
        Name = "Admin",
        Role = AppAccountRole.Admin
    };

    public static readonly AppAccount Worker = new()
    {
        Key = "worker",
        Name = "Worker",
        Role = AppAccountRole.Worker
    };

    public static IReadOnlyList<AppAccount> All { get; } = new[] { Admin, Worker };

    public static AppAccount? Find(string? key)
    {
        return All.FirstOrDefault(account =>
            string.Equals(account.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
