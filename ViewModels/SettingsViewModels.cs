using System.ComponentModel.DataAnnotations;
using DentistDB.Models;
using DentistDB.Services;

namespace DentistDB.ViewModels;

public class SettingsIndexViewModel
{
    public ClinicSettingsFormModel Clinic { get; set; } = new();
    public BackupSettingsFormModel Backup { get; set; } = new();
    public BackupSettings? BackupStatus { get; set; }
    public bool AdminPinIsCustom { get; set; }
    public bool WorkerPinIsCustom { get; set; }
    public int ActiveProcedureCount { get; set; }
    public string DatabasePath { get; set; } = string.Empty;
    public string ScanStoragePath { get; set; } = string.Empty;
    public long DatabaseSizeBytes { get; set; }
    public string AppVersion { get; set; } = string.Empty;
}

public class ClinicSettingsFormModel
{
    [Required(ErrorMessage = "Klinik adı zorunludur.")]
    [MaxLength(80, ErrorMessage = "Klinik adı en fazla 80 karakter olabilir.")]
    [Display(Name = "Klinik Adı")]
    public string ClinicName { get; set; } = "DentistDB";

    [MaxLength(20)]
    [Display(Name = "Unvan")]
    public string? DentistTitle { get; set; } = "Dt.";

    [MaxLength(100)]
    [Display(Name = "Hekim Adı Soyadı")]
    public string? DentistName { get; set; }

    [MaxLength(40)]
    [Display(Name = "Diploma / Tescil No")]
    public string? DiplomaNo { get; set; }

    [MaxLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(40)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [MaxLength(40)]
    [Display(Name = "Vergi No / Dairesi")]
    public string? TaxNo { get; set; }

    [Required]
    [Display(Name = "Çalışma Başlangıcı")]
    [DataType(DataType.Time)]
    public TimeOnly DayStart { get; set; } = new(9, 0);

    [Required]
    [Display(Name = "Çalışma Bitişi")]
    [DataType(DataType.Time)]
    public TimeOnly DayEnd { get; set; } = new(18, 0);
}

public class BackupSettingsFormModel
{
    [Required(ErrorMessage = "Yedek klasörü zorunludur.")]
    [MaxLength(400)]
    [Display(Name = "Yedek Klasörü")]
    public string Folder { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Günlük Yedek Saati")]
    [DataType(DataType.Time)]
    public TimeOnly Time { get; set; } = new(13, 0);

    [Range(1, 365, ErrorMessage = "1 ile 365 arasında olmalıdır.")]
    [Display(Name = "Saklanacak Yedek Sayısı")]
    public int KeepCount { get; set; } = 30;
}

public class ConsentTemplatesFormModel
{
    [Required(ErrorMessage = "Metin boş olamaz.")]
    [MaxLength(8000, ErrorMessage = "En fazla 8000 karakter.")]
    [Display(Name = "Tedavi Onam Formu Metni")]
    public string TreatmentText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Metin boş olamaz.")]
    [MaxLength(8000, ErrorMessage = "En fazla 8000 karakter.")]
    [Display(Name = "KVKK Aydınlatma Metni")]
    public string KvkkText { get; set; } = string.Empty;
}

public class ChangePinViewModel
{
    [Required]
    public AppAccountRole Role { get; set; }

    [Required(ErrorMessage = "Mevcut yönetici PIN'i zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut Yönetici PIN'i")]
    public string CurrentAdminPin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni PIN zorunludur.")]
    [RegularExpression(@"^\d{4,12}$", ErrorMessage = "PIN 4 ile 12 arasında rakamdan oluşmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni PIN")]
    public string NewPin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni PIN'i tekrar girin.")]
    [Compare(nameof(NewPin), ErrorMessage = "PIN'ler eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni PIN (Tekrar)")]
    public string ConfirmPin { get; set; } = string.Empty;
}

public class ProcedureFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "İşlem adı zorunludur.")]
    [MaxLength(150, ErrorMessage = "İşlem adı en fazla 150 karakter olabilir.")]
    [Display(Name = "İşlem Adı")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 9999999.99, ErrorMessage = "Fiyat negatif olamaz.")]
    [Display(Name = "Liste Fiyatı")]
    public decimal DefaultPrice { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public class PriceListViewModel
{
    public IList<Procedure> Procedures { get; set; } = new List<Procedure>();
    public ProcedureFormViewModel NewProcedure { get; set; } = new();
    public bool ShowInactive { get; set; }
}

public class ConnectViewModel
{
    public ServerConnectionInfo Info { get; set; } = null!;
    public string ClinicName { get; set; } = string.Empty;
    /// <summary>Relative path of the iCalendar feed including its secret token.</summary>
    public string CalendarPath { get; set; } = string.Empty;
    public bool IsProduction { get; set; }
}
