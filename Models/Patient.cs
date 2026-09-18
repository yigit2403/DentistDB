using System.ComponentModel.DataAnnotations;

namespace DentistDB.Models;

public class Patient
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    // Nullable because patients imported from the old Access database often have no
    // national ID on file. The unique index tolerates many NULLs on SQLite; the
    // interactive create/edit form still requires a valid TCKN (see PatientFormViewModel).
    [MaxLength(11)]
    [Display(Name = "TCKN")]
    public string? Tckn { get; set; }

    [MaxLength(150)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Display(Name = "Doğum Tarihi")]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "İlk Geliş Tarihi")]
    public DateOnly? ArrivalDate { get; set; }

    [MaxLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [MaxLength(500)]
    [Display(Name = "Tıbbi Uyarılar")]
    public string? MedicalAlerts { get; set; }

    // --- Anamnez (medical history) ---------------------------------------
    [MaxLength(300)]
    [Display(Name = "Alerjiler")]
    public string? Allergies { get; set; }

    [MaxLength(300)]
    [Display(Name = "Kullandığı İlaçlar")]
    public string? Medications { get; set; }

    [Display(Name = "Diyabet")]
    public bool HasDiabetes { get; set; }

    [Display(Name = "Hipertansiyon")]
    public bool HasHypertension { get; set; }

    [Display(Name = "Kalp Hastalığı")]
    public bool HasHeartDisease { get; set; }

    [Display(Name = "Kan Sulandırıcı Kullanıyor")]
    public bool UsesAnticoagulant { get; set; }

    [Display(Name = "Kanama Bozukluğu")]
    public bool HasBleedingDisorder { get; set; }

    [Display(Name = "Hamilelik")]
    public bool IsPregnant { get; set; }

    [Display(Name = "Astım")]
    public bool HasAsthma { get; set; }

    [Display(Name = "Epilepsi")]
    public bool HasEpilepsy { get; set; }

    [Display(Name = "Bulaşıcı Hastalık (Hepatit, HIV vb.)")]
    public bool HasInfectiousDisease { get; set; }

    [Display(Name = "Sigara Kullanıyor")]
    public bool IsSmoker { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Diğer Sağlık Notları")]
    public string? AnamnesisNotes { get; set; }

    public DateTime? AnamnesisUpdatedAt { get; set; }

    [MaxLength(100)]
    [Display(Name = "Acil Durum Kişisi")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    [Display(Name = "Acil Durum Telefonu")]
    public string? EmergencyContactPhone { get; set; }

    [MaxLength(100)]
    [Display(Name = "Veli / Vasi")]
    public string? GuardianName { get; set; }

    public byte[]? PhotoData { get; set; }

    [MaxLength(100)]
    public string? PhotoContentType { get; set; }

    /// <summary>
    /// Lower-cased, diacritic-folded copy of the searchable fields so that
    /// SQLite LIKE queries match Turkish text case-insensitively.
    /// </summary>
    [MaxLength(400)]
    public string SearchIndex { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    /// <summary>Set when the record was brought in from the old Access database.</summary>
    public bool IsImported { get; set; }

    /// <summary>The old program's KisiNumara, kept so a re-import updates rather than duplicates. Null for app-created patients.</summary>
    public int? LegacyKey { get; set; }

    /// <summary>An imported patient whose national ID still needs to be recorded at the next visit.</summary>
    public bool NeedsTckn => IsImported && string.IsNullOrWhiteSpace(Tckn);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<PreviousOperation> PreviousOperations { get; set; } = new List<PreviousOperation>();
    public ICollection<Scan> Scans { get; set; } = new List<Scan>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<ToothStatus> Teeth { get; set; } = new List<ToothStatus>();
    public ICollection<TreatmentPlanItem> TreatmentPlan { get; set; } = new List<TreatmentPlanItem>();
    public ICollection<ConsentRecord> Consents { get; set; } = new List<ConsentRecord>();

    /// <summary>Short list of the anamnesis flags that matter at the chair, for the red banner.</summary>
    public IReadOnlyList<string> RiskFlags
    {
        get
        {
            var flags = new List<string>();
            if (!string.IsNullOrWhiteSpace(Allergies)) flags.Add($"Alerji: {Allergies}");
            if (UsesAnticoagulant) flags.Add("Kan sulandırıcı");
            if (HasBleedingDisorder) flags.Add("Kanama bozukluğu");
            if (HasDiabetes) flags.Add("Diyabet");
            if (HasHypertension) flags.Add("Hipertansiyon");
            if (HasHeartDisease) flags.Add("Kalp hastalığı");
            if (IsPregnant) flags.Add("Hamile");
            if (HasAsthma) flags.Add("Astım");
            if (HasEpilepsy) flags.Add("Epilepsi");
            if (HasInfectiousDisease) flags.Add("Bulaşıcı hastalık");
            if (!string.IsNullOrWhiteSpace(Medications)) flags.Add($"İlaç: {Medications}");
            return flags;
        }
    }

    public bool HasMedicalRisk => !string.IsNullOrWhiteSpace(MedicalAlerts) || RiskFlags.Count > 0;

    public bool HasPhoto => PhotoData is { Length: > 0 } && !string.IsNullOrWhiteSpace(PhotoContentType);

    public int? Age
    {
        get
        {
            if (BirthDate is null) return null;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - BirthDate.Value.Year;
            if (BirthDate.Value > today.AddYears(-age)) age--;
            return age;
        }
    }
}
