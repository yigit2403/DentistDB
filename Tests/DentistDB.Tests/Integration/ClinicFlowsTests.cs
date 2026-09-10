using System.Net;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentistDB.Tests.Integration;

public class ClinicFlowsTests : IClassFixture<ClinicAppFactory>
{
    private readonly ClinicAppFactory _factory;

    public ClinicFlowsTests(ClinicAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousUser_IsRedirectedToAccessPage()
    {
        var client = _factory.CreateBrowser();

        var response = await client.GetAsync("/Patients");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Access", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WrongPin_ShowsError_AndCorrectPin_SignsIn()
    {
        var client = _factory.CreateBrowser();

        var wrong = await client.PostFormAsync("/Access/Select", "/Access", ("AccountKey", "admin"), ("Pin", "0000"));
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("Girilen PIN hatalı", await wrong.ReadAsync());

        await client.LoginAsync("admin", "1234");
        var dashboard = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var html = await dashboard.ReadAsync();
        Assert.Contains("Kontrol Paneli", html);
        Assert.Contains("Bekleyen Alacak", html);
    }

    [Fact]
    public async Task Worker_CannotSeeFinancials_OrReachAdminPages()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("worker", "5678");

        var dashboard = await client.GetAsync("/");
        var html = await dashboard.ReadAsync();
        Assert.DoesNotContain("Bekleyen Alacak", html);
        Assert.DoesNotContain("Ayarlar", html);

        var settings = await client.GetAsync("/Settings");
        Assert.Equal(HttpStatusCode.Redirect, settings.StatusCode);

        var createInvoice = await client.GetAsync("/Billing/Create");
        Assert.Equal(HttpStatusCode.Redirect, createInvoice.StatusCode);

        var invoiceList = await client.GetAsync("/Billing");
        Assert.Equal(HttpStatusCode.OK, invoiceList.StatusCode);
        Assert.DoesNotContain("₺", await invoiceList.ReadAsync());
    }

    [Fact]
    public async Task CreatePatient_ValidatesTcknChecksum_AndDuplicates()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var invalid = await client.PostFormAsync("/Patients/Create", "/Patients/Create",
            ("FullName", "Test Hasta"),
            ("Tckn", "12345678901"));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("TCKN geçerli değil", await invalid.ReadAsync());

        var duplicate = await client.PostFormAsync("/Patients/Create", "/Patients/Create",
            ("FullName", "Test Hasta"),
            ("Tckn", "10000000146"));
        Assert.Contains("zaten", await duplicate.ReadAsync());

        var valid = await client.PostFormAsync("/Patients/Create", "/Patients/Create",
            ("FullName", "Test Hasta"),
            ("Tckn", "12345678950"),
            ("Phone", "0500 000 00 00"),
            ("MedicalAlerts", "Diyabet"));
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);

        var created = _factory.WithDb(db => db.Patients.Single(p => p.Tckn == "12345678950"));
        Assert.Equal("test hasta 12345678950 05000000000 0500 000 00 00", created.SearchIndex);

        var search = await client.GetAsync("/Patients?search=TEST+HASTA");
        Assert.Contains("Test Hasta", await search.ReadAsync());

        var quickSearch = await client.GetAsync("/Patients/Search?q=hast");
        Assert.Contains("\"name\":\"Test Hasta\"", await quickSearch.ReadAsync());
    }

    [Fact]
    public async Task CreateInvoice_WithTurkishDecimals_PersistsItemsAndPaymentPlan()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");
        var patientId = _factory.WithDb(db => db.Patients.OrderBy(p => p.Id).Select(p => p.Id).First());

        var response = await client.PostFormAsync("/Billing/Create", "/Billing/Create",
            ("PatientId", patientId.ToString()),
            ("InvoiceDate", "2026-09-10"),
            ("DueDate", "2026-10-10"),
            ("Status", "Issued"),
            ("Items[0].Description", "Kanal Tedavisi"),
            ("Items[0].ToothNumbers", "26"),
            ("Items[0].Quantity", "1"),
            ("Items[0].UnitPrice", "4.250,50"),
            ("Items[1].Description", "Röntgen"),
            ("Items[1].Quantity", "2"),
            ("Items[1].UnitPrice", "250"),
            ("Items[2].Description", ""),
            ("Items[2].Quantity", "1"),
            ("Items[2].UnitPrice", ""),
            ("EnablePaymentPlan", "true"),
            ("FirstPaymentDate", "2026-10-01"),
            ("InstallmentCount", "3"),
            ("InstallmentIntervalMonths", "1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        var invoiceId = int.Parse(location.Split('/').Last());

        var invoice = _factory.WithDb(db => db.Invoices.Include(i => i.Items).Include(i => i.Payments).Single(i => i.Id == invoiceId));
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal(4750.50m, invoice.TotalAmount);
        Assert.Equal(3, invoice.Payments.Count(p => p.IsPlanned));
        Assert.Equal(4750.50m, invoice.Payments.Where(p => p.IsPlanned).Sum(p => p.Amount));
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);

        var details = await client.GetAsync(location);
        var html = await details.ReadAsync();
        Assert.Contains("₺4.750,50", html);
        Assert.Contains("3 taksit bekliyor", html);
    }

    [Fact]
    public async Task AddPayment_ForInstallment_MarksItSettled_AndUpdatesStatus()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var invoice = _factory.WithDb(db => db.Invoices.Include(i => i.Payments)
            .First(i => i.Status == InvoiceStatus.PartiallyPaid && i.Payments.Any(p => p.IsPlanned && !p.IsSettled)));
        var installment = invoice.Payments.First(p => p.IsPlanned && !p.IsSettled);
        var settledBefore = invoice.Payments.Count(p => p.IsSettled);

        var response = await client.PostFormAsync("/Billing/AddPayment", $"/Billing/AddPayment?invoiceId={invoice.Id}",
            ("InvoiceId", invoice.Id.ToString()),
            ("PlannedPaymentId", installment.Id.ToString()),
            ("PaymentDate", "2026-09-10"),
            ("Amount", installment.Amount.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))),
            ("PaymentMethod", "CreditCard"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var updated = _factory.WithDb(db => db.Invoices.Include(i => i.Payments).Single(i => i.Id == invoice.Id));
        Assert.Equal(settledBefore + 1, updated.Payments.Count(p => p.IsSettled));
        Assert.True(updated.Payments.Single(p => p.Id == installment.Id).IsSettled);
        Assert.Equal(PaymentMethod.CreditCard, updated.Payments.Single(p => p.Id == installment.Id).PaymentMethod);
        Assert.Equal(InvoiceStatus.PartiallyPaid, updated.Status);
    }

    [Fact]
    public async Task CreateAppointment_WarnsOnConflict_AndSavesWhenConfirmed()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var existing = _factory.WithDb(db => db.Appointments.Include(a => a.Patient).First(a => a.Status == AppointmentStatus.Scheduled));
        var otherPatientId = _factory.WithDb(db => db.Patients.Where(p => p.Id != existing.PatientId).Select(p => p.Id).First());
        var date = existing.AppointmentDate.ToString("yyyy-MM-dd");
        var time = existing.AppointmentDate.AddMinutes(10).ToString("HH:mm");

        var conflict = await client.PostFormAsync("/Appointments/Create", "/Appointments/Create",
            ("PatientId", otherPatientId.ToString()),
            ("Date", date),
            ("Time", time),
            ("DurationMinutes", "30"),
            ("Purpose", "Muayene"),
            ("Status", "Scheduled"));

        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        var html = await conflict.ReadAsync();
        Assert.Contains("Bu saat dolu", html);
        Assert.Contains(existing.Patient!.FullName, html);

        var confirmed = await client.PostFormAsync("/Appointments/Create", "/Appointments/Create",
            ("PatientId", otherPatientId.ToString()),
            ("Date", date),
            ("Time", time),
            ("DurationMinutes", "30"),
            ("Purpose", "Muayene"),
            ("Status", "Scheduled"),
            ("IgnoreConflicts", "true"),
            ("SelectedTeeth", "16,17"));

        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);

        var saved = _factory.WithDb(db => db.Appointments.Single(a => a.PatientId == otherPatientId && a.Purpose == "Muayene" && a.AppointmentDate == existing.AppointmentDate.AddMinutes(10)));
        Assert.Equal("16,17", saved.SelectedTeethData);
        Assert.Equal(30, saved.DurationMinutes);

        var conflictsJson = await client.GetAsync($"/Appointments/Conflicts?start={existing.AppointmentDate:yyyy-MM-ddTHH:mm}&duration=30");
        Assert.Contains(existing.Patient.FullName, await conflictsJson.ReadAsync());
    }

    [Fact]
    public async Task SetStatus_CompletesAppointment_AndRespectsReturnUrl()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var appointment = _factory.WithDb(db => db.Appointments.First(a => a.Status == AppointmentStatus.Scheduled && a.Purpose == "Kontrol"));

        var response = await client.PostFormAsync($"/Appointments/SetStatus/{appointment.Id}?status=Completed&returnUrl=%2FPatients%2FDetails%2F{appointment.PatientId}%3Ftab%3Dappointments", "/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Patients/Details/{appointment.PatientId}?tab=appointments", response.Headers.Location!.ToString());
        Assert.Equal(AppointmentStatus.Completed, _factory.WithDb(db => db.Appointments.Single(a => a.Id == appointment.Id).Status));
    }

    [Fact]
    public async Task Settings_ChangePin_RequiresCurrentAdminPin_AndTakesEffect()
    {
        // Own app instance: changing the worker PIN must not affect the other tests sharing the fixture.
        using var factory = new ClinicAppFactory();
        var client = factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var rejected = await client.PostFormAsync("/Settings/ChangePin", "/Settings",
            ("Role", "Worker"),
            ("CurrentAdminPin", "9999"),
            ("NewPin", "246810"),
            ("ConfirmPin", "246810"));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Mevcut yönetici PIN'i hatalı", await rejected.ReadAsync());

        var weak = await client.PostFormAsync("/Settings/ChangePin", "/Settings",
            ("Role", "Worker"),
            ("CurrentAdminPin", "1234"),
            ("NewPin", "1111"),
            ("ConfirmPin", "1111"));
        Assert.Contains("çok zayıf", await weak.ReadAsync());

        var accepted = await client.PostFormAsync("/Settings/ChangePin", "/Settings",
            ("Role", "Worker"),
            ("CurrentAdminPin", "1234"),
            ("NewPin", "246810"),
            ("ConfirmPin", "246810"));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);

        var workerOld = factory.CreateBrowser();
        var oldPinResponse = await workerOld.PostFormAsync("/Access/Select", "/Access", ("AccountKey", "worker"), ("Pin", "5678"));
        Assert.Equal(HttpStatusCode.OK, oldPinResponse.StatusCode);

        var workerNew = factory.CreateBrowser();
        await workerNew.LoginAsync("worker", "246810");
    }

    [Fact]
    public async Task Backup_ReturnsSqliteFile()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var response = await client.PostFormAsync("/Settings/Backup", "/Settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.sqlite3", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("SQLite format 3", System.Text.Encoding.ASCII.GetString(bytes, 0, 15));
    }

    [Fact]
    public async Task UnknownRoute_RendersFriendlyNotFoundPage()
    {
        var client = _factory.CreateBrowser();
        await client.LoginAsync("admin", "1234");

        var response = await client.GetAsync("/Patients/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Aradığınız sayfa bulunamadı", await response.ReadAsync());
    }
}
