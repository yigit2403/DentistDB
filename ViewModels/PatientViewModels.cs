using System.ComponentModel.DataAnnotations;
using DentistDB.Models;

namespace DentistDB.ViewModels;

public class PatientFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "Ad soyad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Telefon en fazla 20 karakter olabilir.")]
    [RegularExpression(@"^[0-9+()\s-]*$", ErrorMessage = "Telefon yalnızca rakam, boşluk, +, ( ) ve - içerebilir.")]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "TCKN zorunludur.")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "TCKN 11 haneli olmalıdır.")]
    [Display(Name = "TCKN")]
    public string Tckn { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [MaxLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Display(Name = "Doğum Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "İlk Geliş Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? ArrivalDate { get; set; }

    [MaxLength(300, ErrorMessage = "Adres en fazla 300 karakter olabilir.")]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(2000, ErrorMessage = "Notlar en fazla 2000 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(500, ErrorMessage = "Tıbbi uyarılar en fazla 500 karakter olabilir.")]
    [Display(Name = "Tıbbi Uyarılar")]
    public string? MedicalAlerts { get; set; }

    [MaxLength(100, ErrorMessage = "En fazla 100 karakter.")]
    [Display(Name = "Acil Durum Kişisi")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20, ErrorMessage = "En fazla 20 karakter.")]
    [RegularExpression(@"^[0-9+()\s-]*$", ErrorMessage = "Telefon yalnızca rakam, boşluk, +, ( ) ve - içerebilir.")]
    [Display(Name = "Acil Durum Telefonu")]
    public string? EmergencyContactPhone { get; set; }

    [MaxLength(100, ErrorMessage = "En fazla 100 karakter.")]
    [Display(Name = "Veli / Vasi")]
    public string? GuardianName { get; set; }

    // Anamnez
    [MaxLength(300, ErrorMessage = "En fazla 300 karakter.")]
    [Display(Name = "Alerjiler")]
    public string? Allergies { get; set; }

    [MaxLength(300, ErrorMessage = "En fazla 300 karakter.")]
    [Display(Name = "Kullandığı İlaçlar")]
    public string? Medications { get; set; }

    [Display(Name = "Diyabet")] public bool HasDiabetes { get; set; }
    [Display(Name = "Hipertansiyon")] public bool HasHypertension { get; set; }
    [Display(Name = "Kalp hastalığı")] public bool HasHeartDisease { get; set; }
    [Display(Name = "Kan sulandırıcı kullanıyor")] public bool UsesAnticoagulant { get; set; }
    [Display(Name = "Kanama bozukluğu")] public bool HasBleedingDisorder { get; set; }
    [Display(Name = "Hamilelik")] public bool IsPregnant { get; set; }
    [Display(Name = "Astım")] public bool HasAsthma { get; set; }
    [Display(Name = "Epilepsi")] public bool HasEpilepsy { get; set; }
    [Display(Name = "Bulaşıcı hastalık (Hepatit, HIV vb.)")] public bool HasInfectiousDisease { get; set; }
    [Display(Name = "Sigara kullanıyor")] public bool IsSmoker { get; set; }

    [MaxLength(1000, ErrorMessage = "En fazla 1000 karakter.")]
    [Display(Name = "Diğer Sağlık Notları")]
    public string? AnamnesisNotes { get; set; }

    [Display(Name = "Hasta Fotoğrafı")]
    public IFormFile? PhotoFile { get; set; }

    [Display(Name = "Mevcut fotoğrafı kaldır")]
    public bool RemovePhoto { get; set; }

    public bool HasExistingPhoto { get; set; }

    [Display(Name = "Arşivde")]
    public bool IsArchived { get; set; }
}

public class PatientListItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Tckn { get; set; }
    public bool NeedsTckn { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public bool HasMedicalAlerts { get; set; }
    public bool HasPhoto { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? NextAppointment { get; set; }
    public DateTime? LastVisit { get; set; }
}

public class PatientIndexViewModel
{
    public string? Search { get; set; }
    public bool ShowArchived { get; set; }
    public PaginatedList<PatientListItem> Patients { get; set; } = new(Enumerable.Empty<PatientListItem>(), 0, 1, 25);
}

public class ToothHistoryEntry
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int? OperationId { get; set; }
}

public class PatientDetailsViewModel
{
    public Patient Patient { get; set; } = null!;
    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();
    public IList<PreviousOperation> Operations { get; set; } = new List<PreviousOperation>();
    public IList<Scan> Scans { get; set; } = new List<Scan>();
    public IList<Invoice> Invoices { get; set; } = new List<Invoice>();
    public IList<ToothStatus> Teeth { get; set; } = new List<ToothStatus>();
    public IList<TreatmentPlanItem> Plan { get; set; } = new List<TreatmentPlanItem>();
    public IList<ConsentRecord> Consents { get; set; } = new List<ConsentRecord>();
    public IReadOnlyList<Procedure> Procedures { get; set; } = Array.Empty<Procedure>();
    public IReadOnlyDictionary<int, List<ToothHistoryEntry>> ToothHistory { get; set; } = new Dictionary<int, List<ToothHistoryEntry>>();
    public bool ShowFinancials { get; set; }
    public string ActiveTab { get; set; } = "overview";

    public Appointment? NextAppointment => Appointments
        .Where(a => a.Status == AppointmentStatus.Scheduled && a.AppointmentDate >= DateTime.Now)
        .OrderBy(a => a.AppointmentDate)
        .FirstOrDefault();

    public DateTime? LastVisit => Appointments
        .Where(a => a.Status == AppointmentStatus.Completed)
        .OrderByDescending(a => a.AppointmentDate)
        .Select(a => (DateTime?)a.AppointmentDate)
        .FirstOrDefault();

    public decimal OutstandingBalance => Invoices
        .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid)
        .Sum(i => i.Balance);

    public decimal TotalBilled => Invoices
        .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
        .Sum(i => i.TotalAmount);

    public IEnumerable<TreatmentPlanItem> OpenPlan => Plan.Where(p => p.Status is TreatmentPlanStatus.Planned or TreatmentPlanStatus.Scheduled);
    public decimal OpenPlanTotal => OpenPlan.Sum(p => p.EstimatedPrice);
    public IEnumerable<TreatmentPlanItem> UnbilledDone => Plan.Where(p => p.Status == TreatmentPlanStatus.Done && p.InvoiceId == null);
}

public class ToothStatusFormModel
{
    public int PatientId { get; set; }

    [Range(11, 85)]
    public int ToothNumber { get; set; }

    public ToothCondition Condition { get; set; }

    [MaxLength(300, ErrorMessage = "Not en fazla 300 karakter olabilir.")]
    public string? Note { get; set; }
}

public class TreatmentPlanFormModel
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? ProcedureId { get; set; }

    [Required(ErrorMessage = "İşlem adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "En fazla 200 karakter.")]
    [Display(Name = "İşlem")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "En fazla 200 karakter.")]
    [Display(Name = "Dişler")]
    public string? ToothNumbers { get; set; }

    [Range(0, 9999999.99, ErrorMessage = "Ücret negatif olamaz.")]
    [Display(Name = "Tahmini Ücret")]
    public decimal EstimatedPrice { get; set; }

    [MaxLength(500, ErrorMessage = "En fazla 500 karakter.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }
}

public class ConsentPrintViewModel
{
    public Patient Patient { get; set; } = null!;
    public ConsentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string ClinicName { get; set; } = string.Empty;
    public Services.ClinicIdentity Identity { get; set; } = null!;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public ConsentRecord? LastSigned { get; set; }
}
