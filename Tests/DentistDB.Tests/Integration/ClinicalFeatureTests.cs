using System.Net;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentistDB.Tests.Integration;

public class ClinicalFeatureTests : IClassFixture<ClinicAppFactory>
{
    private readonly ClinicAppFactory _factory;

    public ClinicalFeatureTests(ClinicAppFactory factory)
    {
        _factory = factory;
    }

    private int FirstPatientId() => _factory.WithDb(db => db.Patients.OrderBy(p => p.Id).Select(p => p.Id).First());

    [Fact]
    public async Task TreatmentPlan_AddScheduleCompleteBill_FlowsThroughRecords()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");
        var patientId = FirstPatientId();
        var procedure = _factory.WithDb(db => db.Procedures.First(p => p.Name.StartsWith("Kanal Tedavisi (Tek")));

        // Add a plan item from the price list.
        var add = await client.PostFormAsync("/TreatmentPlan/Save", $"/Patients/Details/{patientId}?tab=plan",
            ("PatientId", patientId.ToString()),
            ("ProcedureId", procedure.Id.ToString()),
            ("Description", procedure.Name),
            ("ToothNumbers", "26"),
            ("EstimatedPrice", "4.000,00"));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        var item = _factory.WithDb(db => db.TreatmentPlanItems.Single(t => t.PatientId == patientId && t.ProcedureId == procedure.Id));
        Assert.Equal(4000m, item.EstimatedPrice);
        Assert.Equal(TreatmentPlanStatus.Planned, item.Status);

        // Schedule it: the appointment form is pre-filled and links back to the plan item.
        var form = await client.GetAsync($"/Appointments/Create?planItemId={item.Id}");
        var formHtml = await form.ReadAsync();
        Assert.Contains(procedure.Name, formHtml);
        Assert.Contains($"value=\"{item.Id}\"", formHtml);

        var futureDate = DateTime.Today.AddDays(40);
        var schedule = await client.PostFormAsync("/Appointments/Create", $"/Appointments/Create?planItemId={item.Id}",
            ("PatientId", patientId.ToString()),
            ("Date", futureDate.ToString("yyyy-MM-dd")),
            ("Time", "11:00"),
            ("DurationMinutes", "60"),
            ("Purpose", procedure.Name),
            ("Status", "Scheduled"),
            ("PlanItemId", item.Id.ToString()),
            ("SelectedTeeth", "26"));
        Assert.Equal(HttpStatusCode.Redirect, schedule.StatusCode);

        var scheduled = _factory.WithDb(db => db.TreatmentPlanItems.Include(t => t.Appointment).Single(t => t.Id == item.Id));
        Assert.Equal(TreatmentPlanStatus.Scheduled, scheduled.Status);
        Assert.NotNull(scheduled.AppointmentId);
        Assert.Equal(futureDate.AddHours(11), scheduled.Appointment!.AppointmentDate);

        // Complete it: a treatment record is created.
        var complete = await client.PostFormAsync($"/TreatmentPlan/Complete/{item.Id}", $"/Patients/Details/{patientId}?tab=plan");
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);

        var done = _factory.WithDb(db => db.TreatmentPlanItems.Include(t => t.PreviousOperation).Single(t => t.Id == item.Id));
        Assert.Equal(TreatmentPlanStatus.Done, done.Status);
        Assert.NotNull(done.PreviousOperation);
        Assert.Equal("26", done.PreviousOperation!.SelectedTeethData);

        // Bill it: invoice form is pre-filled from the plan item and the item gets linked.
        var invoiceForm = await client.GetAsync($"/Billing/Create?planItems={item.Id}");
        var invoiceHtml = await invoiceForm.ReadAsync();
        Assert.Contains(procedure.Name, invoiceHtml);
        Assert.Contains("4000,00", invoiceHtml);

        var bill = await client.PostFormAsync("/Billing/Create", $"/Billing/Create?planItems={item.Id}",
            ("PatientId", patientId.ToString()),
            ("InvoiceDate", DateTime.Today.ToString("yyyy-MM-dd")),
            ("Status", "Issued"),
            ("PlanItemIds", item.Id.ToString()),
            ("Items[0].Description", procedure.Name),
            ("Items[0].ToothNumbers", "26"),
            ("Items[0].Quantity", "1"),
            ("Items[0].UnitPrice", "4000,00"));
        Assert.Equal(HttpStatusCode.Redirect, bill.StatusCode);

        var billed = _factory.WithDb(db => db.TreatmentPlanItems.Single(t => t.Id == item.Id));
        Assert.NotNull(billed.InvoiceId);
    }

    [Fact]
    public async Task Odontogram_SetTooth_PersistsAndRendersStatus()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("worker", "5678");
        var patientId = FirstPatientId();

        var response = await client.PostFormAsync("/Patients/SetTooth", $"/Patients/Details/{patientId}?tab=odontogram",
            ("PatientId", patientId.ToString()),
            ("ToothNumber", "36"),
            ("Condition", "Implant"),
            ("Note", "2024 implant"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var status = _factory.WithDb(db => db.TeethStatus.Single(t => t.PatientId == patientId && t.ToothNumber == 36));
        Assert.Equal(ToothCondition.Implant, status.Condition);

        var page = await client.GetAsync($"/Patients/Details/{patientId}?tab=odontogram");
        var html = await page.ReadAsync();
        Assert.Contains("&quot;36&quot;:{&quot;condition&quot;:&quot;Implant&quot;", html.Replace("\"", "&quot;"));
        Assert.Contains("2024 implant", html);

        // Setting back to healthy without a note removes the row.
        await client.PostFormAsync("/Patients/SetTooth", $"/Patients/Details/{patientId}?tab=odontogram",
            ("PatientId", patientId.ToString()),
            ("ToothNumber", "36"),
            ("Condition", "Healthy"),
            ("Note", ""));
        Assert.False(_factory.WithDb(db => db.TeethStatus.Any(t => t.PatientId == patientId && t.ToothNumber == 36)));
    }

    [Fact]
    public async Task Anamnesis_FlagsShowUpAsRiskBanner()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var create = await client.PostFormAsync("/Patients/Create", "/Patients/Create",
            ("FullName", "Riskli Hasta"),
            ("Tckn", "10000000214"),
            ("Allergies", "Lidokain"),
            ("UsesAnticoagulant", "true"),
            ("HasDiabetes", "true"));

        // TCKN 10000000214 belongs to the seeded Mehmet Demir, so this must fail as duplicate.
        Assert.Contains("zaten", await create.ReadAsync());

        var created = await client.PostFormAsync("/Patients/Create", "/Patients/Create",
            ("FullName", "Riskli Hasta"),
            ("Tckn", "98765432150"),
            ("Allergies", "Lidokain"),
            ("UsesAnticoagulant", "true"),
            ("HasDiabetes", "true"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var page = await client.GetAsync(created.Headers.Location!.ToString());
        var html = await page.ReadAsync();
        Assert.Contains("Tıbbi uyarı", html);
        Assert.Contains("Alerji: Lidokain", html);
        Assert.Contains("Kan sulandırıcı", html);
        Assert.Contains("Diyabet", html);
    }

    [Fact]
    public async Task ConsentForm_Prints_AndSignatureIsRecorded()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("worker", "5678");
        var patient = _factory.WithDb(db => db.Patients.OrderBy(p => p.Id).First());

        var print = await client.GetAsync($"/Patients/Consent/{patient.Id}?type=Kvkk");
        var html = await print.ReadAsync();
        Assert.Contains("KVKK", html);
        Assert.Contains(patient.FullName, html);
        Assert.Contains(patient.Tckn, html);
        Assert.DoesNotContain("{HastaAdi}", html);

        var record = await client.PostFormAsync("/Patients/RecordConsent", $"/Patients/Details/{patient.Id}?tab=forms",
            ("patientId", patient.Id.ToString()),
            ("type", "Kvkk"),
            ("signedOn", "2026-09-10"));
        Assert.Equal(HttpStatusCode.Redirect, record.StatusCode);
        Assert.True(_factory.WithDb(db => db.ConsentRecords.Any(c => c.PatientId == patient.Id && c.Type == ConsentType.Kvkk)));
    }

    [Fact]
    public async Task Prescription_PrintsForTreatmentRecord()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");
        var op = _factory.WithDb(db => db.PreviousOperations.Include(o => o.Patient).First(o => o.Prescriptions != null));

        var response = await client.GetAsync($"/PreviousOperations/Prescription/{op.Id}");
        var html = await response.ReadAsync();
        Assert.Contains(op.Prescriptions!, html);
        Assert.Contains(op.Patient!.FullName, html);
        Assert.Contains("Reçete", html);
    }

    [Fact]
    public async Task RepeatAppointments_CreateSeries_AndCancelSeries()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");
        var patientId = FirstPatientId();
        var start = DateTime.Today.AddDays(60).Date.AddHours(9);

        var create = await client.PostFormAsync("/Appointments/Create", "/Appointments/Create",
            ("PatientId", patientId.ToString()),
            ("Date", start.ToString("yyyy-MM-dd")),
            ("Time", "09:00"),
            ("DurationMinutes", "30"),
            ("Purpose", "Kanal seansı"),
            ("Status", "Scheduled"),
            ("RepeatCount", "3"),
            ("RepeatIntervalDays", "7"));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        var series = _factory.WithDb(db => db.Appointments.Where(a => a.Purpose == "Kanal seansı").OrderBy(a => a.AppointmentDate).ToList());
        Assert.Equal(4, series.Count);
        Assert.All(series, a => Assert.NotNull(a.SeriesId));
        Assert.Equal(start.AddDays(21), series[^1].AppointmentDate);

        var cancel = await client.PostFormAsync($"/Appointments/CancelSeries/{series[1].Id}", "/Appointments");
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);

        var after = _factory.WithDb(db => db.Appointments.Where(a => a.Purpose == "Kanal seansı").OrderBy(a => a.AppointmentDate).ToList());
        Assert.Equal(AppointmentStatus.Scheduled, after[0].Status);
        Assert.All(after.Skip(1), a => Assert.Equal(AppointmentStatus.Cancelled, a.Status));
    }

    [Fact]
    public async Task DailyReport_PrintDay_AndAuditLog_RenderForAdmin()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var report = await client.GetAsync("/Billing/DailyReport");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Contains("Gün Sonu Raporu", await report.ReadAsync());

        var day = await client.GetAsync($"/Appointments/PrintDay?date={DateTime.Today:yyyy-MM-dd}");
        var dayHtml = await day.ReadAsync();
        Assert.Contains("Günlük randevu listesi", dayHtml);
        Assert.Contains("Ayşe Yılmaz", dayHtml);

        var audit = await client.GetAsync("/Settings/AuditLog");
        var auditHtml = await audit.ReadAsync();
        Assert.Contains("İşlem Günlüğü", auditHtml);
        Assert.Contains("giriş yapıldı", auditHtml);
    }

    [Fact]
    public async Task AuditLog_RecordsEntityChanges_WithAccount()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("worker", "5678");
        var patient = _factory.WithDb(db => db.Patients.OrderBy(p => p.Id).First());

        var edit = await client.PostFormAsync($"/Patients/Edit/{patient.Id}", $"/Patients/Edit/{patient.Id}",
            ("Id", patient.Id.ToString()),
            ("FullName", patient.FullName),
            ("Tckn", patient.Tckn),
            ("Phone", "0500 999 88 77"),
            ("Email", patient.Email ?? ""),
            ("IsArchived", "false"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

        var entry = _factory.WithDb(db => db.AuditEntries.OrderByDescending(a => a.Id).First(a => a.EntityType == "Patient" && a.Action == "Güncellendi"));
        Assert.Equal("worker", entry.Account);
        Assert.Equal(patient.Id, entry.PatientId);
        Assert.Contains("Telefon", entry.Summary);
        Assert.Contains("0500 999 88 77", entry.Details!);
    }

    [Fact]
    public async Task Worker_CannotOpenDailyReportOrAuditLog()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("worker", "5678");

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Billing/DailyReport")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Settings/AuditLog")).StatusCode);
    }

    [Fact]
    public async Task ManualBackupRun_WritesFileToConfiguredFolder()
    {
        using var factory = new ClinicAppFactory();
        var client = factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var folder = Path.Combine(Path.GetTempPath(), "DentistDB.Tests", "backup-" + Guid.NewGuid().ToString("N"));
        var save = await client.PostFormAsync("/Settings/SaveBackup", "/Settings",
            ("Backup.Folder", folder),
            ("Backup.Time", "02:00"),
            ("Backup.KeepCount", "5"));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var run = await client.PostFormAsync("/Settings/RunBackup", "/Settings");
        Assert.Equal(HttpStatusCode.Redirect, run.StatusCode);

        var files = Directory.GetFiles(folder, "*.db");
        Assert.Single(files);
        Assert.StartsWith("SQLite format 3", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(files[0]), 0, 15));

        var settings = await client.GetAsync("/Settings");
        Assert.Contains("Yedek alındı", await settings.ReadAsync());

        Directory.Delete(folder, recursive: true);
    }
}
