using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

public class PatientsController : ClinicControllerBase
{
    private const int PageSize = 25;
    private const long MaxPhotoBytes = 3 * 1024 * 1024;
    private static readonly string[] AllowedPhotoTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] Tabs = { "overview", "appointments", "treatments", "odontogram", "plan", "scans", "invoices", "forms" };

    private readonly ISettingsService _settings;

    public PatientsController(ApplicationDbContext db, ISettingsService settings) : base(db)
    {
        _settings = settings;
    }

    public async Task<IActionResult> Index(string? search, bool showArchived = false, int pageNumber = 1)
    {
        var query = Db.Patients.AsNoTracking().AsQueryable();

        if (!showArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = SearchNormalizer.Normalize(searchTerm);
            var digits = SearchNormalizer.DigitsOnly(searchTerm);
            var pattern = $"%{normalized}%";
            var digitPattern = digits.Length >= 3 ? $"%{digits}%" : null;

            query = query.Where(p =>
                EF.Functions.Like(p.SearchIndex, pattern) ||
                (digitPattern != null && EF.Functions.Like(p.SearchIndex, digitPattern)));
        }

        var now = DateTime.Now;
        var projected = query
            .OrderBy(p => p.FullName)
            .Select(p => new PatientListItem
            {
                Id = p.Id,
                FullName = p.FullName,
                Tckn = p.Tckn,
                Phone = p.Phone,
                Email = p.Email,
                BirthDate = p.BirthDate,
                ArrivalDate = p.ArrivalDate,
                HasMedicalAlerts = (p.MedicalAlerts != null && p.MedicalAlerts != "") || (p.Allergies != null && p.Allergies != "")
                    || p.UsesAnticoagulant || p.HasBleedingDisorder || p.HasDiabetes || p.HasHypertension || p.HasHeartDisease
                    || p.IsPregnant || p.HasAsthma || p.HasEpilepsy || p.HasInfectiousDisease,
                HasPhoto = p.PhotoContentType != null,
                IsArchived = p.IsArchived,
                NextAppointment = p.Appointments
                    .Where(a => a.Status == AppointmentStatus.Scheduled && a.AppointmentDate >= now)
                    .OrderBy(a => a.AppointmentDate)
                    .Select(a => (DateTime?)a.AppointmentDate)
                    .FirstOrDefault(),
                LastVisit = p.Appointments
                    .Where(a => a.Status == AppointmentStatus.Completed)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Select(a => (DateTime?)a.AppointmentDate)
                    .FirstOrDefault()
            });

        var vm = new PatientIndexViewModel
        {
            Search = searchTerm,
            ShowArchived = showArchived,
            Patients = await PaginatedList<PatientListItem>.CreateAsync(projected, pageNumber, PageSize)
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int id, string? tab)
    {
        var patient = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        var operations = await Db.PreviousOperations.AsNoTracking()
            .Where(o => o.PatientId == id)
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Id)
            .ToListAsync();

        var appointments = await Db.Appointments.AsNoTracking()
            .Where(a => a.PatientId == id)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        var vm = new PatientDetailsViewModel
        {
            Patient = patient,
            ShowFinancials = HttpContext.CanViewFinancials(),
            ActiveTab = Tabs.Contains(tab) ? tab! : "overview",
            Appointments = appointments,
            Operations = operations,
            Scans = await Db.Scans.AsNoTracking()
                .Where(s => s.PatientId == id)
                .OrderByDescending(s => s.ScanDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync(),
            Invoices = await Db.Invoices.AsNoTracking()
                .Include(i => i.Payments)
                .Include(i => i.Items)
                .Where(i => i.PatientId == id)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync(),
            Teeth = await Db.TeethStatus.AsNoTracking()
                .Where(t => t.PatientId == id)
                .OrderBy(t => t.ToothNumber)
                .ToListAsync(),
            Plan = await Db.TreatmentPlanItems.AsNoTracking()
                .Include(t => t.Appointment)
                .Where(t => t.PatientId == id)
                .OrderBy(t => t.Status == TreatmentPlanStatus.Cancelled ? 2 : t.Status == TreatmentPlanStatus.Done ? 1 : 0)
                .ThenBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .ToListAsync(),
            Consents = await Db.ConsentRecords.AsNoTracking()
                .Where(c => c.PatientId == id)
                .OrderByDescending(c => c.SignedOn)
                .ThenByDescending(c => c.Id)
                .ToListAsync(),
            Procedures = await Db.Procedures.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .ToListAsync(),
            ToothHistory = BuildToothHistory(operations, appointments)
        };

        return View(vm);
    }

    private static Dictionary<int, List<ToothHistoryEntry>> BuildToothHistory(IEnumerable<PreviousOperation> operations, IEnumerable<Appointment> appointments)
    {
        var history = new Dictionary<int, List<ToothHistoryEntry>>();

        void Add(string? teeth, ToothHistoryEntry entry)
        {
            foreach (var t in TeethSelectionSerializer.Parse(teeth))
            {
                if (!int.TryParse(t, out var number)) continue;
                if (!history.TryGetValue(number, out var list))
                {
                    list = new List<ToothHistoryEntry>();
                    history[number] = list;
                }
                list.Add(entry);
            }
        }

        foreach (var op in operations)
        {
            Add(op.SelectedTeethData, new ToothHistoryEntry { Date = op.Date, Title = op.Title, Source = "Tedavi", OperationId = op.Id });
        }

        foreach (var appt in appointments.Where(a => a.Status == AppointmentStatus.Completed))
        {
            Add(appt.SelectedTeethData, new ToothHistoryEntry { Date = DateOnly.FromDateTime(appt.AppointmentDate), Title = appt.Purpose, Source = "Randevu" });
        }

        foreach (var list in history.Values)
        {
            list.Sort((a, b) => b.Date.CompareTo(a.Date));
        }

        return history;
    }

    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Photo(int id)
    {
        var photo = await Db.Patients.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.PhotoData, p.PhotoContentType })
            .FirstOrDefaultAsync();

        if (photo?.PhotoData is null || string.IsNullOrEmpty(photo.PhotoContentType))
        {
            return NotFound();
        }

        return File(photo.PhotoData, photo.PhotoContentType);
    }

    public IActionResult Create() => View(new PatientFormViewModel
    {
        ArrivalDate = DateOnly.FromDateTime(DateTime.Today)
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PatientFormViewModel vm)
    {
        NormalizeInput(vm);
        await ValidateAsync(vm);
        if (!ModelState.IsValid) return View(vm);

        var patient = new Patient { CreatedAt = DateTime.UtcNow };
        ApplyForm(patient, vm);

        if (vm.PhotoFile is { Length: > 0 })
        {
            patient.PhotoData = await ReadPhotoAsync(vm.PhotoFile);
            patient.PhotoContentType = vm.PhotoFile.ContentType;
        }

        Db.Patients.Add(patient);
        await Db.SaveChangesAsync();
        Success($"{patient.FullName} için hasta kartı oluşturuldu.");
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var patient = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        var vm = new PatientFormViewModel
        {
            Id = patient.Id,
            FullName = patient.FullName,
            Phone = patient.Phone,
            Tckn = patient.Tckn,
            Email = patient.Email,
            BirthDate = patient.BirthDate,
            ArrivalDate = patient.ArrivalDate,
            Address = patient.Address,
            Notes = patient.Notes,
            MedicalAlerts = patient.MedicalAlerts,
            EmergencyContactName = patient.EmergencyContactName,
            EmergencyContactPhone = patient.EmergencyContactPhone,
            GuardianName = patient.GuardianName,
            Allergies = patient.Allergies,
            Medications = patient.Medications,
            HasDiabetes = patient.HasDiabetes,
            HasHypertension = patient.HasHypertension,
            HasHeartDisease = patient.HasHeartDisease,
            UsesAnticoagulant = patient.UsesAnticoagulant,
            HasBleedingDisorder = patient.HasBleedingDisorder,
            IsPregnant = patient.IsPregnant,
            HasAsthma = patient.HasAsthma,
            HasEpilepsy = patient.HasEpilepsy,
            HasInfectiousDisease = patient.HasInfectiousDisease,
            IsSmoker = patient.IsSmoker,
            AnamnesisNotes = patient.AnamnesisNotes,
            HasExistingPhoto = patient.HasPhoto,
            IsArchived = patient.IsArchived
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, PatientFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        var patient = await Db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        NormalizeInput(vm);
        await ValidateAsync(vm, id);

        if (!ModelState.IsValid)
        {
            vm.HasExistingPhoto = patient.HasPhoto;
            return View(vm);
        }

        ApplyForm(patient, vm);
        patient.IsArchived = vm.IsArchived;

        if (vm.PhotoFile is { Length: > 0 })
        {
            patient.PhotoData = await ReadPhotoAsync(vm.PhotoFile);
            patient.PhotoContentType = vm.PhotoFile.ContentType;
        }
        else if (vm.RemovePhoto)
        {
            patient.PhotoData = null;
            patient.PhotoContentType = null;
        }

        await Db.SaveChangesAsync();
        Success("Hasta bilgileri güncellendi.");
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    private static void ApplyForm(Patient patient, PatientFormViewModel vm)
    {
        var anamnesisChanged = patient.Id == 0
            || patient.Allergies != vm.Allergies || patient.Medications != vm.Medications
            || patient.HasDiabetes != vm.HasDiabetes || patient.HasHypertension != vm.HasHypertension
            || patient.HasHeartDisease != vm.HasHeartDisease || patient.UsesAnticoagulant != vm.UsesAnticoagulant
            || patient.HasBleedingDisorder != vm.HasBleedingDisorder || patient.IsPregnant != vm.IsPregnant
            || patient.HasAsthma != vm.HasAsthma || patient.HasEpilepsy != vm.HasEpilepsy
            || patient.HasInfectiousDisease != vm.HasInfectiousDisease || patient.IsSmoker != vm.IsSmoker
            || patient.AnamnesisNotes != vm.AnamnesisNotes;

        patient.FullName = vm.FullName;
        patient.Phone = vm.Phone;
        patient.Tckn = vm.Tckn;
        patient.Email = vm.Email;
        patient.BirthDate = vm.BirthDate;
        patient.ArrivalDate = vm.ArrivalDate;
        patient.Address = vm.Address;
        patient.Notes = vm.Notes;
        patient.MedicalAlerts = vm.MedicalAlerts;
        patient.EmergencyContactName = vm.EmergencyContactName;
        patient.EmergencyContactPhone = vm.EmergencyContactPhone;
        patient.GuardianName = vm.GuardianName;
        patient.Allergies = vm.Allergies;
        patient.Medications = vm.Medications;
        patient.HasDiabetes = vm.HasDiabetes;
        patient.HasHypertension = vm.HasHypertension;
        patient.HasHeartDisease = vm.HasHeartDisease;
        patient.UsesAnticoagulant = vm.UsesAnticoagulant;
        patient.HasBleedingDisorder = vm.HasBleedingDisorder;
        patient.IsPregnant = vm.IsPregnant;
        patient.HasAsthma = vm.HasAsthma;
        patient.HasEpilepsy = vm.HasEpilepsy;
        patient.HasInfectiousDisease = vm.HasInfectiousDisease;
        patient.IsSmoker = vm.IsSmoker;
        patient.AnamnesisNotes = vm.AnamnesisNotes;
        if (anamnesisChanged)
        {
            patient.AnamnesisUpdatedAt = DateTime.UtcNow;
        }

        patient.SearchIndex = SearchNormalizer.BuildPatientIndex(patient);
        patient.UpdatedAt = DateTime.UtcNow;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var patient = await Db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IsArchived = true;
        patient.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        Success($"{patient.FullName} arşive taşındı.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var patient = await Db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IsArchived = false;
        patient.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        Success($"{patient.FullName} yeniden aktif edildi.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Odontogram ---------------------------------------------------------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTooth(ToothStatusFormModel model)
    {
        var patientExists = await Db.Patients.AnyAsync(p => p.Id == model.PatientId);
        if (!patientExists) return NotFound();

        if (!ModelState.IsValid)
        {
            Error("Diş bilgisi kaydedilemedi; alanları kontrol edin.");
            return RedirectToAction(nameof(Details), new { id = model.PatientId, tab = "odontogram" });
        }

        var status = await Db.TeethStatus.FirstOrDefaultAsync(t => t.PatientId == model.PatientId && t.ToothNumber == model.ToothNumber);
        var note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();

        if (model.Condition == ToothCondition.Healthy && note == null)
        {
            if (status != null)
            {
                Db.TeethStatus.Remove(status);
            }
        }
        else if (status == null)
        {
            Db.TeethStatus.Add(new ToothStatus
            {
                PatientId = model.PatientId,
                ToothNumber = model.ToothNumber,
                Condition = model.Condition,
                Note = note,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            status.Condition = model.Condition;
            status.Note = note;
            status.UpdatedAt = DateTime.UtcNow;
        }

        await Db.SaveChangesAsync();
        Success($"{model.ToothNumber} numaralı diş güncellendi: {model.Condition.GetDisplayName()}.");
        return RedirectToAction(nameof(Details), new { id = model.PatientId, tab = "odontogram" });
    }

    // ---- Consent forms ------------------------------------------------------

    public async Task<IActionResult> Consent(int id, ConsentType type = ConsentType.Treatment)
    {
        var patient = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        var clinic = await _settings.GetClinicSettingsAsync();
        var identity = await _settings.GetClinicIdentityAsync();
        var template = type == ConsentType.Kvkk
            ? await _settings.GetAsync(AppSetting.ConsentKvkkText) ?? ConsentTemplates.KvkkDefault
            : await _settings.GetAsync(AppSetting.ConsentTreatmentText) ?? ConsentTemplates.TreatmentDefault;

        var vm = new ConsentPrintViewModel
        {
            Patient = patient,
            Type = type,
            Title = type.GetDisplayName(),
            ClinicName = clinic.ClinicName,
            Identity = identity,
            Date = DateOnly.FromDateTime(DateTime.Today),
            BodyText = ConsentTemplates.Render(template, patient, clinic, identity, DateOnly.FromDateTime(DateTime.Today)),
            LastSigned = await Db.ConsentRecords.AsNoTracking()
                .Where(c => c.PatientId == id && c.Type == type)
                .OrderByDescending(c => c.SignedOn)
                .FirstOrDefaultAsync()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordConsent(int patientId, ConsentType type, DateOnly? signedOn, string? notes)
    {
        var exists = await Db.Patients.AnyAsync(p => p.Id == patientId);
        if (!exists) return NotFound();

        Db.ConsentRecords.Add(new ConsentRecord
        {
            PatientId = patientId,
            Type = type,
            SignedOn = signedOn ?? DateOnly.FromDateTime(DateTime.Today),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
        await Db.SaveChangesAsync();

        Success($"{type.GetDisplayName()} imzalandı olarak kaydedildi.");
        return RedirectToAction(nameof(Details), new { id = patientId, tab = "forms" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConsent(int id)
    {
        var record = await Db.ConsentRecords.FindAsync(id);
        if (record == null) return NotFound();

        Db.ConsentRecords.Remove(record);
        await Db.SaveChangesAsync();
        Success("Onam kaydı silindi.");
        return RedirectToAction(nameof(Details), new { id = record.PatientId, tab = "forms" });
    }

    /// <summary>Lightweight JSON endpoint used by the topbar quick search.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        var term = q?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            return Json(Array.Empty<object>());
        }

        var normalized = SearchNormalizer.Normalize(term);
        var pattern = $"%{normalized}%";

        var results = await Db.Patients.AsNoTracking()
            .Where(p => !p.IsArchived && EF.Functions.Like(p.SearchIndex, pattern))
            .OrderBy(p => p.FullName)
            .Take(8)
            .Select(p => new
            {
                id = p.Id,
                name = p.FullName,
                phone = p.Phone,
                tckn = p.Tckn,
                alerts = (p.MedicalAlerts != null && p.MedicalAlerts != "") || (p.Allergies != null && p.Allergies != "") || p.UsesAnticoagulant || p.HasBleedingDisorder
            })
            .ToListAsync();

        return Json(results);
    }

    private static void NormalizeInput(PatientFormViewModel vm)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        vm.FullName = (vm.FullName ?? string.Empty).Trim();
        vm.Tckn = SearchNormalizer.DigitsOnly(vm.Tckn);
        vm.Phone = Clean(vm.Phone);
        vm.Email = Clean(vm.Email);
        vm.Address = Clean(vm.Address);
        vm.Notes = Clean(vm.Notes);
        vm.MedicalAlerts = Clean(vm.MedicalAlerts);
        vm.EmergencyContactName = Clean(vm.EmergencyContactName);
        vm.EmergencyContactPhone = Clean(vm.EmergencyContactPhone);
        vm.GuardianName = Clean(vm.GuardianName);
        vm.Allergies = Clean(vm.Allergies);
        vm.Medications = Clean(vm.Medications);
        vm.AnamnesisNotes = Clean(vm.AnamnesisNotes);
    }

    private async Task ValidateAsync(PatientFormViewModel vm, int? currentPatientId = null)
    {
        if (vm.Tckn.Length == 11 && !TcknValidator.IsValid(vm.Tckn))
        {
            ModelState.AddModelError(nameof(vm.Tckn), "TCKN geçerli değil; rakamları kontrol edin.");
        }

        if (ModelState.GetValidationState(nameof(vm.Tckn)) == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid)
        {
            var duplicate = await Db.Patients.AsNoTracking()
                .Where(p => p.Tckn == vm.Tckn && (!currentPatientId.HasValue || p.Id != currentPatientId.Value))
                .Select(p => new { p.Id, p.FullName })
                .FirstOrDefaultAsync();

            if (duplicate != null)
            {
                ModelState.AddModelError(nameof(vm.Tckn), $"Bu TCKN zaten {duplicate.FullName} adlı hastada kayıtlı.");
            }
        }

        if (vm.BirthDate is { } birth && birth > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(vm.BirthDate), "Doğum tarihi gelecekte olamaz.");
        }

        if (vm.PhotoFile is { Length: > 0 } photo)
        {
            if (photo.Length > MaxPhotoBytes)
            {
                ModelState.AddModelError(nameof(vm.PhotoFile), "Fotoğraf en fazla 3 MB olabilir.");
            }

            if (!AllowedPhotoTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(vm.PhotoFile), "Yalnızca JPG, PNG veya WebP fotoğraf yükleyebilirsiniz.");
            }
        }
    }

    private static async Task<byte[]> ReadPhotoAsync(IFormFile photoFile)
    {
        await using var stream = new MemoryStream();
        await photoFile.CopyToAsync(stream);
        return stream.ToArray();
    }
}
