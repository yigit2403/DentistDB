using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentistDB.Tests.Integration;

/// <summary>Dedicated app instance so the destructive checks below cannot disturb the other test classes.</summary>
public sealed class SweepFactory : ClinicAppFactory { }

/// <summary>
/// Exhaustive pass over every page and every form action, as both accounts.
/// Anything that returns a 500 or leaks an exception message fails here.
/// </summary>
public class FullSweepTests : IClassFixture<SweepFactory>
{
    private readonly SweepFactory _factory;

    public FullSweepTests(SweepFactory factory)
    {
        _factory = factory;
    }

    private static readonly byte[] TinyBmp = Convert.FromBase64String("Qk1GAAAAAAAAADYAAAAoAAAAAgAAAAIAAAABABgAAAAAABAAAAATCwAAEwsAAAAAAAAAAAAA////AAAAAAD///8AAAAAAA==");

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private async Task<HttpClient> AdminAsync()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");
        return client;
    }

    private async Task<HttpClient> WorkerAsync()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("worker", "5678");
        return client;
    }

    private static async Task<string> OkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.ReadAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} → {(int)response.StatusCode}\n{Head(body)}");
        AssertNoServerError(url, body);
        return body;
    }

    private static void AssertNoServerError(string url, string body)
    {
        Assert.False(body.Contains("An unhandled exception occurred") || body.Contains("Beklenmeyen bir hata oluştu") || body.Contains("Exception:"),
            $"{url} rendered an error page:\n{Head(body)}");
    }

    private static string Head(string body) => body.Length > 600 ? body[..600] : body;

    private int PatientId(int index = 0) => _factory.WithDb(db => db.Patients.OrderBy(p => p.Id).Skip(index).Select(p => p.Id).First());

    // ------------------------------------------------------------------------------------------
    // GET sweep
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Admin_CanOpenEveryPage()
    {
        var client = await AdminAsync();
        var patientId = PatientId();
        var appointmentId = _factory.WithDb(db => db.Appointments.OrderBy(a => a.Id).Select(a => a.Id).First());
        var operationId = _factory.WithDb(db => db.PreviousOperations.OrderBy(o => o.Id).Select(o => o.Id).First());
        var invoiceId = _factory.WithDb(db => db.Invoices.OrderBy(i => i.Id).Select(i => i.Id).First());
        var openInvoiceId = _factory.WithDb(db => db.Invoices.Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid).Select(i => i.Id).First());
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var urls = new List<string>
        {
            "/", "/Home/Index",
            "/Patients", "/Patients?search=ay", "/Patients?showArchived=true&pageNumber=2", "/Patients?search=0532",
            "/Patients/Create", $"/Patients/Edit/{patientId}", $"/Patients/Search?q=ay",
            $"/Patients/Consent/{patientId}", $"/Patients/Consent/{patientId}?type=Kvkk",
            "/Appointments", $"/Appointments?view=Weekly&date={today}", $"/Appointments?view=Monthly&date={today}",
            $"/Appointments?view=List&date={today}", $"/Appointments?status=Scheduled&patientId={patientId}",
            "/Appointments?view=Monthly&date=2026-02-01", "/Appointments?view=Weekly&date=2026-12-31",
            "/Appointments/Create", $"/Appointments/Create?patientId={patientId}&date={today}T14:30&purpose=Kontrol&teeth=16,17&duration=15",
            $"/Appointments/Create?date={today}", $"/Appointments/Edit/{appointmentId}", $"/Appointments/Edit/{appointmentId}?returnUrl=%2F",
            $"/Appointments/Conflicts?start={today}T09:00&duration=30", $"/Appointments/PrintDay?date={today}", "/Appointments/PrintDay",
            "/PreviousOperations", $"/PreviousOperations?patientId={patientId}", "/PreviousOperations?search=dolgu", "/PreviousOperations?search=26",
            "/PreviousOperations/Create", $"/PreviousOperations/Create?patientId={patientId}", $"/PreviousOperations/Edit/{operationId}",
            $"/PreviousOperations/Prescription/{operationId}",
            $"/Scans/Upload?patientId={patientId}",
            "/Billing", "/Billing?status=Paid", "/Billing?overdue=true", "/Billing?search=%231", "/Billing?search=zeynep", $"/Billing?patientId={patientId}",
            $"/Billing/Details/{invoiceId}", "/Billing/Create", $"/Billing/Create?patientId={patientId}", $"/Billing/Edit/{invoiceId}",
            $"/Billing/AddPayment?invoiceId={openInvoiceId}", "/Billing/DailyReport", "/Billing/DailyReport?date=2026-08-11",
            "/Settings", "/Settings/PriceList", "/Settings/PriceList?showInactive=true", "/Settings/Consents",
            "/Settings/AuditLog", "/Settings/AuditLog?account=admin&entityType=Oturum&search=giri%C5%9F&from=2026-01-01&to=2026-12-31",
            "/Settings/Connect",
            "/manifest.webmanifest", "/images/icon-512.png", "/images/apple-touch-icon.png", "/images/teethSelection.svg",
            "/js/tooth-chart.js", "/js/tooth-selector.js", "/js/odontogram.js", "/js/scan-viewer.js", "/js/invoice-form.js", "/js/appointment-form.js",
            "/css/site.css", "/css/print.css", "/css/scan-viewer.css", "/lib/qrcode/qrcode.min.js"
        };

        foreach (var tab in new[] { "overview", "odontogram", "plan", "appointments", "treatments", "scans", "invoices", "forms", "bogus" })
        {
            urls.Add($"/Patients/Details/{patientId}?tab={tab}");
        }

        foreach (var url in urls)
        {
            await OkAsync(client, url);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Patients/Details/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Appointments/Edit/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Billing/Details/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Patients/Photo/{patientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Scans/View/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/PreviousOperations/Prescription/999999")).StatusCode);
    }

    [Fact]
    public async Task Worker_SeesClinicalPages_ButNotAdminOnes()
    {
        var client = await WorkerAsync();
        var patientId = PatientId();
        var invoiceId = _factory.WithDb(db => db.Invoices.OrderBy(i => i.Id).Select(i => i.Id).First());

        foreach (var url in new[] { "/", "/Patients", $"/Patients/Details/{patientId}?tab=plan", $"/Patients/Details/{patientId}?tab=invoices", "/Appointments", "/Appointments/Create", "/PreviousOperations", $"/Billing/Details/{invoiceId}", $"/Patients/Consent/{patientId}" })
        {
            var body = await OkAsync(client, url);
            Assert.DoesNotContain("₺", body);
        }

        foreach (var url in new[] { "/Settings", "/Settings/PriceList", "/Settings/Consents", "/Settings/AuditLog", "/Settings/Connect", "/Billing/Create", $"/Billing/Edit/{invoiceId}", $"/Billing/AddPayment?invoiceId={invoiceId}", "/Billing/DailyReport" })
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"{url} should redirect the worker, got {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Anonymous_IsBlockedEverywhere_ExceptAccessAndStaticFiles()
    {
        var client = _factory.CreateBrowser();
        foreach (var url in new[] { "/", "/Patients", "/Appointments", "/Billing", "/Settings", "/Settings/Connect", "/Calendar/Feed" })
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.NotFound, $"{url} → {(int)response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Access")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/css/site.css")).StatusCode);
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejected()
    {
        var client = await AdminAsync();
        var response = await client.PostAsync("/Patients/Create", new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("FullName", "x") }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_LocksOutAfterFiveFailures_AndLogoutWorks()
    {
        using var factory = new SweepFactory();
        var client = factory.CreateBrowser();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostFormAsync("/Access/Select", "/Access", ("AccountKey", "admin"), ("Pin", "0000"));
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        }

        var locked = await client.PostFormAsync("/Access/Select", "/Access", ("AccountKey", "admin"), ("Pin", "1234"));
        Assert.Contains("Çok fazla hatalı deneme", await locked.ReadAsync());

        // A different client (same in-memory throttle keyed by IP) is also locked; that's expected on one PC.
        var other = factory.CreateBrowser();
        await other.LoginAsync("worker", "5678").ContinueWith(t => Assert.True(t.IsFaulted || t.IsCompletedSuccessfully));

        var admin = factory.CreateBrowser();
        var logout = await admin.PostFormAsync("/Access/Logout", "/Access");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // Patients
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Patient_Photo_Upload_Serve_Remove_Archive_Restore()
    {
        var client = await AdminAsync();
        var patientId = PatientId(1);
        var patient = _factory.WithDb(db => db.Patients.Single(p => p.Id == patientId));

        var token = await client.GetTokenAsync($"/Patients/Edit/{patientId}");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(patientId.ToString()), "Id" },
            { new StringContent(patient.FullName), "FullName" },
            { new StringContent(patient.Tckn), "Tckn" },
            { new StringContent(patient.Phone ?? ""), "Phone" },
            { new StringContent(patient.Email ?? ""), "Email" },
            { new StringContent("false"), "IsArchived" }
        };
        var png = new ByteArrayContent(TinyPng);
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(png, "PhotoFile", "foto.png");

        var upload = await client.PostAsync($"/Patients/Edit/{patientId}", form);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);

        var photo = await client.GetAsync($"/Patients/Photo/{patientId}");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);

        var list = await OkAsync(client, "/Patients");
        Assert.Contains($"/Patients/Photo/{patientId}", list);

        // Remove the photo again.
        var remove = await client.PostFormAsync($"/Patients/Edit/{patientId}", $"/Patients/Edit/{patientId}",
            ("Id", patientId.ToString()), ("FullName", patient.FullName), ("Tckn", patient.Tckn), ("Phone", patient.Phone ?? ""), ("Email", patient.Email ?? ""), ("RemovePhoto", "true"), ("IsArchived", "false"));
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Patients/Photo/{patientId}")).StatusCode);

        // Archive → hidden from appointment form → restore.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Patients/Archive/{patientId}", $"/Patients/Details/{patientId}")).StatusCode);
        Assert.True(_factory.WithDb(db => db.Patients.Single(p => p.Id == patientId).IsArchived));
        var create = await OkAsync(client, "/Appointments/Create");
        Assert.DoesNotContain($">{patient.FullName} ·", create);
        var archivedList = await OkAsync(client, "/Patients");
        Assert.DoesNotContain($"class=\"person-name\">{patient.FullName}<", archivedList);
        Assert.Contains(patient.FullName, await OkAsync(client, "/Patients?showArchived=true"));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Patients/Restore/{patientId}", $"/Patients/Details/{patientId}")).StatusCode);
        Assert.False(_factory.WithDb(db => db.Patients.Single(p => p.Id == patientId).IsArchived));
    }

    [Fact]
    public async Task Patient_Create_RejectsBadEmail_FuturePhoto_AndBadPhotoType()
    {
        var client = await AdminAsync();

        var blank = await client.PostFormAsync("/Patients/Create", "/Patients/Create", ("FullName", ""), ("Tckn", ""));
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        Assert.Contains("Ad soyad zorunludur", await blank.ReadAsync());

        var badEmail = await client.PostFormAsync("/Patients/Create", "/Patients/Create", ("FullName", "Test"), ("Tckn", "12345678950"), ("Email", "not-an-email"), ("BirthDate", "2999-01-01"));
        var html = await badEmail.ReadAsync();
        Assert.Equal(HttpStatusCode.OK, badEmail.StatusCode);
        Assert.Contains("Geçerli bir e-posta", html);
        Assert.Contains("gelecekte olamaz", html);

        var token = await client.GetTokenAsync("/Patients/Create");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Test Foto"), "FullName" },
            { new StringContent("12345678950"), "Tckn" }
        };
        var exe = new ByteArrayContent(new byte[] { 1, 2, 3 });
        exe.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(exe, "PhotoFile", "virus.exe");
        var badPhoto = await client.PostAsync("/Patients/Create", form);
        Assert.Contains("JPG, PNG veya WebP", await badPhoto.ReadAsync());
        Assert.False(_factory.WithDb(db => db.Patients.Any(p => p.FullName == "Test Foto")));
    }

    [Fact]
    public async Task Patient_Pagination_RendersSecondPage()
    {
        using var factory = new SweepFactory();
        var client = factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        factory.WithDb(db =>
        {
            for (var i = 0; i < 30; i++)
            {
                var digits = $"9{i:D8}";
                db.Patients.Add(new Patient { FullName = $"Sayfa Hasta {i:D2}", Tckn = ValidTckn(digits), SearchIndex = $"sayfa hasta {i:D2}" });
            }
            return db.SaveChanges();
        });

        var page2 = await OkAsync(client, "/Patients?pageNumber=2");
        Assert.Contains("26–34 arası", page2);
        Assert.Contains("pageNumber=1", page2);
    }

    private static string ValidTckn(string first9)
    {
        var d = first9.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var d10 = ((odd * 7) - even) % 10;
        if (d10 < 0) d10 += 10;
        var d11 = (d.Sum() + d10) % 10;
        return first9 + d10 + d11;
    }

    // ------------------------------------------------------------------------------------------
    // Appointments
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Appointment_Edit_StatusChanges_AndDelete()
    {
        var client = await AdminAsync();
        var patientId = PatientId(2);
        var day = DateTime.Today.AddDays(70);

        var create = await client.PostFormAsync("/Appointments/Create", "/Appointments/Create",
            ("PatientId", patientId.ToString()), ("Date", day.ToString("yyyy-MM-dd")), ("Time", "10:00"), ("DurationMinutes", "30"), ("Purpose", "Sweep"), ("Status", "Scheduled"));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var id = _factory.WithDb(db => db.Appointments.Single(a => a.Purpose == "Sweep").Id);

        // Edit: move to 10:30 with 45 min, change teeth.
        var edit = await client.PostFormAsync($"/Appointments/Edit/{id}", $"/Appointments/Edit/{id}",
            ("Id", id.ToString()), ("PatientId", patientId.ToString()), ("Date", day.ToString("yyyy-MM-dd")), ("Time", "10:30"), ("DurationMinutes", "45"), ("Purpose", "Sweep düzenlendi"), ("Status", "Scheduled"), ("SelectedTeeth", "36, 37"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        var edited = _factory.WithDb(db => db.Appointments.Single(a => a.Id == id));
        Assert.Equal(day.AddHours(10).AddMinutes(30), edited.AppointmentDate);
        Assert.Equal(45, edited.DurationMinutes);
        Assert.Equal("36,37", edited.SelectedTeethData);

        // Editing an appointment onto itself must not report a conflict with itself.
        var same = await client.PostFormAsync($"/Appointments/Edit/{id}", $"/Appointments/Edit/{id}",
            ("Id", id.ToString()), ("PatientId", patientId.ToString()), ("Date", day.ToString("yyyy-MM-dd")), ("Time", "10:30"), ("DurationMinutes", "45"), ("Purpose", "Sweep düzenlendi"), ("Status", "Scheduled"));
        Assert.Equal(HttpStatusCode.Redirect, same.StatusCode);

        // Mismatched id in the form body vs the route → 400.
        var mismatch = await client.PostFormAsync($"/Appointments/Edit/{id}", $"/Appointments/Edit/{id}", ("Id", "999"), ("PatientId", patientId.ToString()), ("Date", day.ToString("yyyy-MM-dd")), ("Time", "10:30"), ("DurationMinutes", "45"), ("Purpose", "x"), ("Status", "Scheduled"));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        foreach (var status in new[] { "NoShow", "Cancelled", "Scheduled", "Completed" })
        {
            var set = await client.PostFormAsync($"/Appointments/SetStatus/{id}?status={status}", "/Appointments");
            Assert.Equal(HttpStatusCode.Redirect, set.StatusCode);
            Assert.Equal(Enum.Parse<AppointmentStatus>(status), _factory.WithDb(db => db.Appointments.Single(a => a.Id == id).Status));
        }

        // Completed appointment renders the follow-up menu on the agenda.
        var agenda = await OkAsync(client, $"/Appointments?date={day:yyyy-MM-dd}");
        Assert.Contains("Kontrol randevusu", agenda);
        Assert.Contains("Geri Al", agenda);

        // Worker may not delete; admin may.
        var worker = await WorkerAsync();
        var denied = await worker.PostFormAsync($"/Appointments/Delete/{id}", "/Appointments");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.True(_factory.WithDb(db => db.Appointments.Any(a => a.Id == id)));

        var deleted = await client.PostFormAsync($"/Appointments/Delete/{id}", "/Appointments");
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.False(_factory.WithDb(db => db.Appointments.Any(a => a.Id == id)));
    }

    [Fact]
    public async Task Appointment_Create_FromSlotLink_PrefillsDateAndTime()
    {
        var client = await AdminAsync();

        // A slot link passes date=…T09:15; the raw query value must not leak into the type="date" input.
        var html = await OkAsync(client, "/Appointments/Create?date=2026-09-10T09:15");
        Assert.Matches("name=\"Date\"[^>]*value=\"2026-09-10\"", html);
        Assert.Matches("name=\"Time\"[^>]*value=\"09:15\"", html);
    }

    [Fact]
    public async Task Appointment_TimeGrid_RendersSlotsAndPlacesAppointments()
    {
        var client = await AdminAsync();
        var appointment = _factory.WithDb(db => db.Appointments.OrderBy(a => a.Id).First());
        var day = appointment.AppointmentDate.Date;

        var daily = await OkAsync(client, $"/Appointments?view=Daily&date={day:yyyy-MM-dd}");
        Assert.Contains("data-time-grid", daily);
        Assert.Contains($"date={day:yyyy-MM-dd}T09%3A00", daily.Replace("&amp;", "&"));
        Assert.Contains("class=\"tg-event tg-event--full", daily);
        Assert.Contains("grid-row:", daily);

        var weekly = await OkAsync(client, $"/Appointments?view=Weekly&date={day:yyyy-MM-dd}");
        Assert.Equal(7, Regex.Matches(weekly, "class=\"tg-day").Count);
        Assert.Contains($"data-tg-day=\"{day:yyyy-MM-dd}\"", weekly);
    }

    [Fact]
    public async Task Appointment_Create_ValidationErrors_RenderForm()
    {
        var client = await AdminAsync();
        var response = await client.PostFormAsync("/Appointments/Create", "/Appointments/Create", ("PatientId", "0"), ("Date", "2026-09-10"), ("Time", "09:00"), ("DurationMinutes", "30"), ("Purpose", ""), ("Status", "Scheduled"));
        var html = await response.ReadAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hasta seçimi zorunludur", html);
        Assert.Contains("Randevu nedeni zorunludur", html);
        AssertNoServerError("/Appointments/Create", html);
    }

    // ------------------------------------------------------------------------------------------
    // Treatment records and plan
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task TreatmentRecord_Create_Edit_Delete_AndPlanReorder()
    {
        var client = await AdminAsync();
        var patientId = PatientId(2);

        var create = await client.PostFormAsync("/PreviousOperations/Create", "/PreviousOperations/Create",
            ("PatientId", patientId.ToString()), ("Date", "2026-09-01"), ("Title", "Sweep tedavi"), ("Diagnosis", "Tanı"), ("Procedures", "İşlem"), ("Prescriptions", "Parol 500"), ("Notes", "not"), ("SelectedTeeth", "11"));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var opId = _factory.WithDb(db => db.PreviousOperations.Single(o => o.Title == "Sweep tedavi").Id);

        var edit = await client.PostFormAsync($"/PreviousOperations/Edit/{opId}", $"/PreviousOperations/Edit/{opId}",
            ("Id", opId.ToString()), ("PatientId", patientId.ToString()), ("Date", "2026-09-02"), ("Title", "Sweep tedavi 2"), ("Prescriptions", "Parol 500"), ("SelectedTeeth", "11,12"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Equal("11,12", _factory.WithDb(db => db.PreviousOperations.Single(o => o.Id == opId).SelectedTeethData));

        var invalid = await client.PostFormAsync("/PreviousOperations/Create", "/PreviousOperations/Create", ("PatientId", patientId.ToString()), ("Date", "2026-09-01"), ("Title", ""));
        Assert.Contains("İşlem başlığı zorunludur", await invalid.ReadAsync());

        Assert.Contains("Sweep tedavi 2", await OkAsync(client, $"/PreviousOperations/Prescription/{opId}"));
        Assert.Contains("Parol 500", await OkAsync(client, $"/PreviousOperations/Prescription/{opId}"));

        // Plan: two items, reorder, cancel/reopen, invalid save, delete permissions.
        foreach (var name in new[] { "Plan A", "Plan B" })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/TreatmentPlan/Save", $"/Patients/Details/{patientId}?tab=plan", ("PatientId", patientId.ToString()), ("Description", name), ("EstimatedPrice", "100"))).StatusCode);
        }
        var items = _factory.WithDb(db => db.TreatmentPlanItems.Where(t => t.PatientId == patientId && t.Description.StartsWith("Plan ")).OrderBy(t => t.SortOrder).ToList());
        Assert.Equal(new[] { "Plan A", "Plan B" }, items.Select(i => i.Description));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/TreatmentPlan/Move/{items[1].Id}?direction=-1", $"/Patients/Details/{patientId}?tab=plan")).StatusCode);
        var reordered = _factory.WithDb(db => db.TreatmentPlanItems.Where(t => t.PatientId == patientId && t.Description.StartsWith("Plan ")).OrderBy(t => t.SortOrder).Select(t => t.Description).ToList());
        Assert.Equal(new[] { "Plan B", "Plan A" }, reordered);

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/TreatmentPlan/SetStatus/{items[0].Id}?status=Cancelled", "/")).StatusCode);
        Assert.Equal(TreatmentPlanStatus.Cancelled, _factory.WithDb(db => db.TreatmentPlanItems.Single(t => t.Id == items[0].Id).Status));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/TreatmentPlan/SetStatus/{items[0].Id}?status=Planned", "/")).StatusCode);
        Assert.Equal(TreatmentPlanStatus.Planned, _factory.WithDb(db => db.TreatmentPlanItems.Single(t => t.Id == items[0].Id).Status));

        // Edit existing item through Save with Id.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/TreatmentPlan/Save", "/", ("Id", items[0].Id.ToString()), ("PatientId", patientId.ToString()), ("Description", "Plan A2"), ("ToothNumbers", "21, 22"), ("EstimatedPrice", "1.250,50"))).StatusCode);
        var editedItem = _factory.WithDb(db => db.TreatmentPlanItems.Single(t => t.Id == items[0].Id));
        Assert.Equal("Plan A2", editedItem.Description);
        Assert.Equal("21,22", editedItem.ToothNumbers);
        Assert.Equal(1250.50m, editedItem.EstimatedPrice);

        var invalidPlan = await client.PostFormAsync("/TreatmentPlan/Save", "/", ("PatientId", patientId.ToString()), ("Description", ""), ("EstimatedPrice", "abc"));
        Assert.Equal(HttpStatusCode.Redirect, invalidPlan.StatusCode);
        var planPage = await OkAsync(client, $"/Patients/Details/{patientId}?tab=plan");
        Assert.Contains("Plan kalemi kaydedilemedi", planPage);

        var worker = await WorkerAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await worker.PostFormAsync($"/TreatmentPlan/Delete/{items[1].Id}", "/")).StatusCode);
        Assert.True(_factory.WithDb(db => db.TreatmentPlanItems.Any(t => t.Id == items[1].Id)));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/TreatmentPlan/Delete/{items[1].Id}", "/")).StatusCode);
        Assert.False(_factory.WithDb(db => db.TreatmentPlanItems.Any(t => t.Id == items[1].Id)));

        // Worker cannot delete treatment records; admin can.
        Assert.Equal(HttpStatusCode.Redirect, (await worker.PostFormAsync($"/PreviousOperations/Delete/{opId}", "/")).StatusCode);
        Assert.True(_factory.WithDb(db => db.PreviousOperations.Any(o => o.Id == opId)));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/PreviousOperations/Delete/{opId}", "/")).StatusCode);
        Assert.False(_factory.WithDb(db => db.PreviousOperations.Any(o => o.Id == opId)));
    }

    // ------------------------------------------------------------------------------------------
    // Scans
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Scan_Upload_View_Content_Edit_Delete()
    {
        var client = await AdminAsync();
        var patientId = PatientId(3);

        async Task<HttpResponseMessage> UploadAsync(HttpClient c, byte[] bytes, string fileName, string contentType)
        {
            var token = await c.GetTokenAsync($"/Scans/Upload?patientId={patientId}");
            var form = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent(patientId.ToString()), "PatientId" },
                { new StringContent("Panoramic"), "ScanType" },
                { new StringContent("2026-09-10"), "ScanDate" },
                { new StringContent("Sweep notu"), "Notes" }
            };
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "File", fileName);
            return await c.PostAsync("/Scans/Upload", form);
        }

        var bad = await UploadAsync(client, new byte[] { 1, 2 }, "x.exe", "application/octet-stream");
        Assert.Contains("JPG, PNG, WebP, BMP ve PDF", await bad.ReadAsync());

        // Extension decides the type; the header must match it.
        var mismatch = await UploadAsync(client, TinyPng, "x.pdf", "image/png");
        Assert.Contains("uzantısıyla uyuşmuyor", await mismatch.ReadAsync());

        var dicom = await UploadAsync(client, new byte[] { 1, 2, 3, 4 }, "pano.dcm", "application/dicom");
        Assert.Contains("DICOM", await dicom.ReadAsync());

        // BMP exported by panoramic device software is accepted and served as image/bmp regardless of the browser's content type.
        var bmpOk = await UploadAsync(client, TinyBmp, "PANO.BMP", "application/octet-stream");
        Assert.Equal(HttpStatusCode.Redirect, bmpOk.StatusCode);
        var bmpScan = _factory.WithDb(db => db.Scans.Single(s => s.PatientId == patientId && s.FileName == "PANO.BMP"));
        Assert.Equal("image/bmp", bmpScan.ContentType);
        var bmpContent = await client.GetAsync($"/Scans/ContentFile/{bmpScan.Id}");
        Assert.Equal("image/bmp", bmpContent.Content.Headers.ContentType!.MediaType);

        var empty = await client.PostFormAsync("/Scans/Upload", $"/Scans/Upload?patientId={patientId}", ("PatientId", patientId.ToString()), ("ScanType", "Panoramic"), ("ScanDate", "2026-09-10"));
        Assert.Contains("bir dosya seçin", await empty.ReadAsync());

        var ok = await UploadAsync(client, TinyPng, "rontgen.png", "image/png");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        var scan = _factory.WithDb(db => db.Scans.Single(s => s.PatientId == patientId && s.FileName == "rontgen.png"));

        var view = await OkAsync(client, $"/Scans/View/{scan.Id}");
        Assert.Contains("Sweep notu", view);
        Assert.Contains("scan-viewer.js", view);

        var content = await client.GetAsync($"/Scans/ContentFile/{scan.Id}");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/png", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal(TinyPng.Length, (await content.Content.ReadAsByteArrayAsync()).Length);

        var download = await client.GetAsync($"/Scans/ContentFile/{scan.Id}?download=true");
        Assert.Contains("rontgen.png", download.Content.Headers.ContentDisposition!.ToString());

        Assert.Contains($"/Scans/ContentFile/{scan.Id}", await OkAsync(client, $"/Patients/Details/{patientId}?tab=scans"));

        var edit = await client.PostFormAsync($"/Scans/Edit/{scan.Id}", $"/Scans/Edit/{scan.Id}", ("Id", scan.Id.ToString()), ("PatientId", patientId.ToString()), ("ScanType", "Bitewing"), ("ScanDate", "2026-09-09"), ("Notes", "Düzenlendi"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        var editedScan = _factory.WithDb(db => db.Scans.Single(s => s.Id == scan.Id));
        Assert.Equal(ScanType.Bitewing, editedScan.ScanType);
        Assert.Equal("Düzenlendi", editedScan.Notes);

        var worker = await WorkerAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await worker.PostFormAsync($"/Scans/Delete/{scan.Id}", "/")).StatusCode);
        Assert.True(_factory.WithDb(db => db.Scans.Any(s => s.Id == scan.Id)));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Scans/Delete/{scan.Id}", "/")).StatusCode);
        Assert.False(_factory.WithDb(db => db.Scans.Any(s => s.Id == scan.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Scans/ContentFile/{scan.Id}")).StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // Billing
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Invoice_Edit_Payments_Cancel_Reopen_Delete()
    {
        var client = await AdminAsync();
        var patientId = PatientId(3);

        var create = await client.PostFormAsync("/Billing/Create", "/Billing/Create",
            ("PatientId", patientId.ToString()), ("InvoiceDate", "2026-09-10"), ("DueDate", "2026-10-10"), ("Status", "Issued"),
            ("Items[0].Description", "Muayene"), ("Items[0].Quantity", "1"), ("Items[0].UnitPrice", "500"),
            ("Items[1].Description", "Dolgu"), ("Items[1].Quantity", "2"), ("Items[1].UnitPrice", "1000"),
            ("EnablePaymentPlan", "true"), ("FirstPaymentDate", "2026-10-01"), ("InstallmentCount", "2"), ("InstallmentIntervalMonths", "1"));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var invoiceId = int.Parse(create.Headers.Location!.ToString().Split('/').Last());
        var invoice = _factory.WithDb(db => db.Invoices.Include(i => i.Items).Include(i => i.Payments).Single(i => i.Id == invoiceId));
        Assert.Equal(2500m, invoice.TotalAmount);
        Assert.Equal(2, invoice.Payments.Count(p => p.IsPlanned));

        // Empty items → validation error.
        var noItems = await client.PostFormAsync("/Billing/Create", "/Billing/Create", ("PatientId", patientId.ToString()), ("InvoiceDate", "2026-09-10"), ("Status", "Issued"), ("Items[0].Description", ""), ("Items[0].Quantity", "1"), ("Items[0].UnitPrice", ""));
        Assert.Contains("en az bir işlem satırı", await noItems.ReadAsync());

        // Plan enabled with 1 installment → error.
        var badPlan = await client.PostFormAsync("/Billing/Create", "/Billing/Create", ("PatientId", patientId.ToString()), ("InvoiceDate", "2026-09-10"), ("Status", "Issued"), ("Items[0].Description", "X"), ("Items[0].Quantity", "1"), ("Items[0].UnitPrice", "10"), ("EnablePaymentPlan", "true"), ("FirstPaymentDate", "2026-10-01"), ("InstallmentCount", "1"), ("InstallmentIntervalMonths", "1"));
        Assert.Contains("en az 2 taksit", await badPlan.ReadAsync());

        // Edit: drop one item, change price, regenerate plan into 3 installments (nothing settled yet).
        var muayene = invoice.Items.Single(i => i.Description == "Muayene");
        var edit = await client.PostFormAsync($"/Billing/Edit/{invoiceId}", $"/Billing/Edit/{invoiceId}",
            ("Id", invoiceId.ToString()), ("PatientId", patientId.ToString()), ("InvoiceDate", "2026-09-10"), ("DueDate", "2026-10-10"), ("Status", "Issued"),
            ("Items[0].Id", muayene.Id.ToString()), ("Items[0].Description", "Muayene"), ("Items[0].Quantity", "1"), ("Items[0].UnitPrice", "600,00"),
            ("Items[1].Description", "Röntgen"), ("Items[1].Quantity", "1"), ("Items[1].UnitPrice", "300"),
            ("EnablePaymentPlan", "true"), ("FirstPaymentDate", "2026-10-01"), ("InstallmentCount", "3"), ("InstallmentIntervalMonths", "1"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        invoice = _factory.WithDb(db => db.Invoices.Include(i => i.Items).Include(i => i.Payments).Single(i => i.Id == invoiceId));
        Assert.Equal(900m, invoice.TotalAmount);
        Assert.Equal(2, invoice.Items.Count);
        Assert.DoesNotContain(invoice.Items, i => i.Description == "Dolgu");
        Assert.Equal(3, invoice.Payments.Count(p => p.IsPlanned));
        Assert.Equal(900m, invoice.Payments.Where(p => p.IsPlanned).Sum(p => p.Amount));

        // Cancel needs no payments → allowed; reopen restores Issued.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/Cancel/{invoiceId}", "/")).StatusCode);
        Assert.Equal(InvoiceStatus.Cancelled, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));
        var addToCancelled = await client.GetAsync($"/Billing/AddPayment?invoiceId={invoiceId}");
        Assert.Equal(HttpStatusCode.Redirect, addToCancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/Reopen/{invoiceId}", "/")).StatusCode);
        Assert.Equal(InvoiceStatus.Issued, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));

        // Settle installment 1, then an unplanned payment for the rest → Paid.
        var first = invoice.Payments.Where(p => p.IsPlanned).OrderBy(p => p.InstallmentNumber).First();
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/Billing/AddPayment", $"/Billing/AddPayment?invoiceId={invoiceId}", ("InvoiceId", invoiceId.ToString()), ("PlannedPaymentId", first.Id.ToString()), ("PaymentDate", "2026-09-10"), ("Amount", "300,00"), ("PaymentMethod", "Cash"))).StatusCode);
        Assert.Equal(InvoiceStatus.PartiallyPaid, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));

        // Plan is now locked: edit must keep the plan even if the form asks to change it.
        var lockedEdit = await client.PostFormAsync($"/Billing/Edit/{invoiceId}", $"/Billing/Edit/{invoiceId}",
            ("Id", invoiceId.ToString()), ("PatientId", patientId.ToString()), ("InvoiceDate", "2026-09-10"), ("Status", "Issued"),
            ("Items[0].Description", "Tek kalem"), ("Items[0].Quantity", "1"), ("Items[0].UnitPrice", "900"),
            ("EnablePaymentPlan", "true"), ("FirstPaymentDate", "2026-10-01"), ("InstallmentCount", "6"), ("InstallmentIntervalMonths", "1"));
        Assert.Equal(HttpStatusCode.Redirect, lockedEdit.StatusCode);
        Assert.Equal(3, _factory.WithDb(db => db.Payments.Count(p => p.InvoiceId == invoiceId && p.IsPlanned)));

        // Cancel/delete refused while paid.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/Cancel/{invoiceId}", "/")).StatusCode);
        Assert.Equal(InvoiceStatus.PartiallyPaid, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/Delete/{invoiceId}", "/")).StatusCode);
        Assert.True(_factory.WithDb(db => db.Invoices.Any(i => i.Id == invoiceId)));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/Billing/AddPayment", $"/Billing/AddPayment?invoiceId={invoiceId}", ("InvoiceId", invoiceId.ToString()), ("PaymentDate", "2026-09-10"), ("Amount", "600"), ("PaymentMethod", "CreditCard"), ("Notes", "Kalan"))).StatusCode);
        Assert.Equal(InvoiceStatus.Paid, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));

        var details = await OkAsync(client, $"/Billing/Details/{invoiceId}");
        Assert.Contains("₺900,00", details);
        Assert.Contains("Kalan", details);
        Assert.DoesNotContain("Tahsilat Ekle", details);

        // Invalid payment (zero, unknown installment).
        var invalidPay = await client.PostFormAsync("/Billing/AddPayment", $"/Billing/AddPayment?invoiceId={invoiceId}", ("InvoiceId", invoiceId.ToString()), ("PaymentDate", "2026-09-10"), ("Amount", "0"), ("PaymentMethod", "Cash"), ("PlannedPaymentId", "999999"));
        var invalidHtml = await invalidPay.ReadAsync();
        Assert.Contains("sıfırdan büyük", invalidHtml);
        Assert.Contains("bulunamadı", invalidHtml);

        // Delete the unplanned payment → back to PartiallyPaid; un-settle installment → Issued.
        var unplanned = _factory.WithDb(db => db.Payments.Single(p => p.InvoiceId == invoiceId && !p.IsPlanned));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/DeletePayment/{unplanned.Id}", "/")).StatusCode);
        Assert.False(_factory.WithDb(db => db.Payments.Any(p => p.Id == unplanned.Id)));
        Assert.Equal(InvoiceStatus.PartiallyPaid, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/DeletePayment/{first.Id}", "/")).StatusCode);
        var unsettled = _factory.WithDb(db => db.Payments.Single(p => p.Id == first.Id));
        Assert.False(unsettled.IsSettled);
        Assert.Equal(InvoiceStatus.Issued, _factory.WithDb(db => db.Invoices.Single(i => i.Id == invoiceId).Status));

        // Now nothing is paid: delete works and plan items are gone with it.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Billing/Delete/{invoiceId}", "/")).StatusCode);
        Assert.False(_factory.WithDb(db => db.Invoices.Any(i => i.Id == invoiceId)));
        Assert.False(_factory.WithDb(db => db.Payments.Any(p => p.InvoiceId == invoiceId)));

        // Worker cannot post billing changes.
        var worker = await WorkerAsync();
        var seeded = _factory.WithDb(db => db.Invoices.OrderBy(i => i.Id).Select(i => i.Id).First());
        Assert.Equal(HttpStatusCode.Redirect, (await worker.PostFormAsync($"/Billing/Cancel/{seeded}", "/")).StatusCode);
        Assert.NotEqual(InvoiceStatus.Cancelled, _factory.WithDb(db => db.Invoices.Single(i => i.Id == seeded).Status));
    }

    [Fact]
    public async Task DailyReport_ReflectsPayments()
    {
        var client = await AdminAsync();
        var paidDate = _factory.WithDb(db => db.Payments.Where(p => p.IsSettled).Select(p => p.SettledDate ?? p.PaymentDate).First());
        var report = await OkAsync(client, $"/Billing/DailyReport?date={paidDate:yyyy-MM-dd}");
        Assert.Contains("Tahsilatlar", report);
        Assert.DoesNotContain("Bu gün tahsilat yapılmadı", report);
        Assert.Contains("Ödeme Yöntemine Göre", report);
    }

    // ------------------------------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Settings_Clinic_Consents_PriceList_Backup()
    {
        var client = await AdminAsync();

        var badHours = await client.PostFormAsync("/Settings/SaveClinic", "/Settings", ("Clinic.ClinicName", "Test Klinik"), ("Clinic.DayStart", "18:00"), ("Clinic.DayEnd", "09:00"));
        Assert.Equal(HttpStatusCode.OK, badHours.StatusCode);
        Assert.Contains("başlangıçtan sonra", await badHours.ReadAsync());

        var saveClinic = await client.PostFormAsync("/Settings/SaveClinic", "/Settings", ("Clinic.ClinicName", "Sweep Klinik"), ("Clinic.DentistTitle", "Dt."), ("Clinic.DentistName", "Ali Veli"), ("Clinic.DiplomaNo", "1234"), ("Clinic.Address", "Konya"), ("Clinic.Phone", "0332 000 00 00"), ("Clinic.DayStart", "08:30"), ("Clinic.DayEnd", "19:00"));
        Assert.Equal(HttpStatusCode.Redirect, saveClinic.StatusCode);
        var dashboard = await OkAsync(client, "/");
        Assert.Contains("Sweep Klinik", dashboard);
        var prescriptionId = _factory.WithDb(db => db.PreviousOperations.OrderBy(o => o.Id).Select(o => o.Id).First());
        Assert.Contains("Dt. Ali Veli", await OkAsync(client, $"/PreviousOperations/Prescription/{prescriptionId}"));
        Assert.Contains("value=\"08:30\"", await OkAsync(client, "/Appointments/Create"));

        // Consent templates: save custom, render, reset.
        var saveConsents = await client.PostFormAsync("/Settings/Consents", "/Settings/Consents", ("TreatmentText", "Özel onam {HastaAdi} için."), ("KvkkText", "Özel KVKK {KlinikAdi}."));
        Assert.Equal(HttpStatusCode.Redirect, saveConsents.StatusCode);
        var patientId = PatientId();
        var patientName = _factory.WithDb(db => db.Patients.Single(p => p.Id == patientId).FullName);
        Assert.Contains($"Özel onam {patientName} için.", await OkAsync(client, $"/Patients/Consent/{patientId}?type=Treatment"));
        Assert.Contains("Özel KVKK Sweep Klinik.", await OkAsync(client, $"/Patients/Consent/{patientId}?type=Kvkk"));
        var reset = await client.PostFormAsync("/Settings/Consents", "/Settings/Consents", ("reset", "treatment"), ("TreatmentText", "x"), ("KvkkText", "y"));
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Contains("olası riskleri", await OkAsync(client, $"/Patients/Consent/{patientId}?type=Treatment"));
        var emptyConsent = await client.PostFormAsync("/Settings/Consents", "/Settings/Consents", ("TreatmentText", ""), ("KvkkText", ""));
        Assert.Equal(HttpStatusCode.OK, emptyConsent.StatusCode);

        // Price list: add, duplicate, edit, toggle, delete unused, delete used → deactivated.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/Settings/SaveProcedure", "/Settings/PriceList", ("Id", "0"), ("Name", "Sweep İşlem"), ("DefaultPrice", "1.500,00"), ("IsActive", "true"))).StatusCode);
        var proc = _factory.WithDb(db => db.Procedures.Single(p => p.Name == "Sweep İşlem"));
        Assert.Equal(1500m, proc.DefaultPrice);
        var dup = await client.PostFormAsync("/Settings/SaveProcedure", "/Settings/PriceList", ("Id", "0"), ("Name", "sweep işlem"), ("DefaultPrice", "1"), ("IsActive", "true"));
        Assert.Contains("zaten var", await dup.ReadAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/Settings/SaveProcedure", "/Settings/PriceList", ("Id", proc.Id.ToString()), ("Name", "Sweep İşlem 2"), ("DefaultPrice", "1700"), ("IsActive", "true"))).StatusCode);
        Assert.Equal(1700m, _factory.WithDb(db => db.Procedures.Single(p => p.Id == proc.Id).DefaultPrice));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Settings/ToggleProcedure/{proc.Id}", "/Settings/PriceList")).StatusCode);
        Assert.False(_factory.WithDb(db => db.Procedures.Single(p => p.Id == proc.Id).IsActive));
        Assert.DoesNotContain("<td class=\"fw-600\">Sweep İşlem 2</td>", await OkAsync(client, "/Settings/PriceList"));
        Assert.Contains("Sweep İşlem 2", await OkAsync(client, "/Settings/PriceList?showInactive=true"));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Settings/DeleteProcedure/{proc.Id}", "/Settings/PriceList")).StatusCode);
        Assert.False(_factory.WithDb(db => db.Procedures.Any(p => p.Id == proc.Id)));

        var usedProc = _factory.WithDb(db => db.InvoiceItems.Where(i => i.ProcedureId != null).Select(i => i.ProcedureId!.Value).First());
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Settings/DeleteProcedure/{usedProc}", "/Settings/PriceList")).StatusCode);
        var stillThere = _factory.WithDb(db => db.Procedures.Single(p => p.Id == usedProc));
        Assert.False(stillThere.IsActive);

        // Backup settings validation + download.
        var badFolder = await client.PostFormAsync("/Settings/SaveBackup", "/Settings", ("Backup.Folder", ""), ("Backup.Time", "13:00"), ("Backup.KeepCount", "0"));
        Assert.Equal(HttpStatusCode.OK, badFolder.StatusCode);
        Assert.Contains("zorunludur", await badFolder.ReadAsync());
        var download = await client.PostFormAsync("/Settings/Backup", "/Settings");
        Assert.Equal("application/vnd.sqlite3", download.Content.Headers.ContentType!.MediaType);

        // Admin PIN change through the admin panel path, then log in with it.
        using var isolated = new SweepFactory();
        var isoClient = isolated.CreateBrowser();
        await isoClient.LoginAsync("admin", "1234");
        var change = await isoClient.PostFormAsync("/Settings/ChangePin", "/Settings", ("Role", "Admin"), ("CurrentAdminPin", "1234"), ("NewPin", "735913"), ("ConfirmPin", "735913"));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        var fresh = isolated.CreateBrowser();
        await fresh.LoginAsync("admin", "735913");
        var mismatchPin = await fresh.PostFormAsync("/Settings/ChangePin", "/Settings", ("Role", "Admin"), ("CurrentAdminPin", "735913"), ("NewPin", "111222"), ("ConfirmPin", "333444"));
        Assert.Contains("eşleşmiyor", await mismatchPin.ReadAsync());
    }

    [Fact]
    public async Task ConsentRecord_Delete_AndWorkerRestriction()
    {
        var client = await AdminAsync();
        var patientId = PatientId(1);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/Patients/RecordConsent", "/", ("patientId", patientId.ToString()), ("type", "Treatment"), ("signedOn", "2026-09-10"), ("notes", "sweep"))).StatusCode);
        var record = _factory.WithDb(db => db.ConsentRecords.Single(c => c.PatientId == patientId && c.Notes == "sweep"));
        Assert.Contains("İmzalandı 10.09.2026", await OkAsync(client, $"/Patients/Details/{patientId}?tab=forms"));

        var worker = await WorkerAsync();
        var workerPage = await OkAsync(worker, $"/Patients/Details/{patientId}?tab=forms");
        Assert.DoesNotContain($"/Patients/DeleteConsent/{record.Id}", workerPage);

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync($"/Patients/DeleteConsent/{record.Id}", "/")).StatusCode);
        Assert.False(_factory.WithDb(db => db.ConsentRecords.Any(c => c.Id == record.Id)));
    }

    [Fact]
    public async Task OdontogramPage_EmbedsStatusesAndHistoryAsJson()
    {
        var client = await AdminAsync();
        var patientId = PatientId();
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostFormAsync("/Patients/SetTooth", "/", ("PatientId", patientId.ToString()), ("ToothNumber", "55"), ("Condition", "Caries"), ("Note", "süt dişi"))).StatusCode);

        var invalid = await client.PostFormAsync("/Patients/SetTooth", "/", ("PatientId", patientId.ToString()), ("ToothNumber", "99"), ("Condition", "Caries"));
        Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);
        Assert.False(_factory.WithDb(db => db.TeethStatus.Any(t => t.PatientId == patientId && t.ToothNumber == 99)));

        var page = await OkAsync(client, $"/Patients/Details/{patientId}?tab=odontogram");
        Assert.Contains("data-statuses=", page);
        Assert.Contains("süt dişi", page);
        Assert.Contains("data-history=", page);
        Assert.Contains("Kompozit dolgu", page);
    }
}
