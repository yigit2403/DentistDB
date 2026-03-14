namespace DentistDB.Models;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? ScanStoragePath { get; set; }
    public string? DataProtectionKeysPath { get; set; }
}
