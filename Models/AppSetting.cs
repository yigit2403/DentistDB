using System.ComponentModel.DataAnnotations;

namespace DentistDB.Models;

/// <summary>Key/value store for settings that are edited inside the app (PIN hashes, clinic name, ...).</summary>
public class AppSetting
{
    public const string AdminPinHash = "Pin:Admin";
    public const string WorkerPinHash = "Pin:Worker";

    public const string ClinicName = "Clinic:Name";
    public const string ClinicDentistName = "Clinic:DentistName";
    public const string ClinicDentistTitle = "Clinic:DentistTitle";
    public const string ClinicDiplomaNo = "Clinic:DiplomaNo";
    public const string ClinicAddress = "Clinic:Address";
    public const string ClinicPhone = "Clinic:Phone";
    public const string ClinicTaxNo = "Clinic:TaxNo";
    public const string WorkDayStart = "Schedule:DayStart";
    public const string WorkDayEnd = "Schedule:DayEnd";

    public const string ConsentTreatmentText = "Consent:Treatment";
    public const string ConsentKvkkText = "Consent:Kvkk";

    public const string BackupFolder = "Backup:Folder";
    public const string BackupTime = "Backup:Time";
    public const string BackupKeepCount = "Backup:KeepCount";
    public const string BackupLastRunAt = "Backup:LastRunAt";
    public const string BackupLastResult = "Backup:LastResult";

    [Key, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(8000)]
    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
