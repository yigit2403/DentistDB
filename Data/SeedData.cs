using DentistDB.Models;
using DentistDB.Services;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Data;

public static class SeedData
{
    /// <summary>Reference data that every installation needs (safe to run in production).</summary>
    public static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        if (!await db.Procedures.AnyAsync())
        {
            var defaults = new (string Name, decimal Price)[]
            {
                ("Muayene", 500m),
                ("Kontrol", 0m),
                ("Diş Taşı Temizliği (Detertraj)", 1500m),
                ("Kompozit Dolgu", 2000m),
                ("Amalgam Dolgu", 1500m),
                ("Kanal Tedavisi (Tek Kanal)", 4000m),
                ("Kanal Tedavisi (Çok Kanal)", 6000m),
                ("Diş Çekimi", 1500m),
                ("Gömülü Diş Çekimi", 5000m),
                ("İmplant", 25000m),
                ("Zirkonyum Kaplama", 9000m),
                ("Porselen Kaplama", 7000m),
                ("Hareketli Protez", 15000m),
                ("Diş Beyazlatma", 6000m),
                ("Panoramik Röntgen", 600m),
                ("Periapikal Röntgen", 250m),
                ("Fissür Örtücü", 800m),
                ("Flor Uygulaması", 700m)
            };

            var order = 0;
            db.Procedures.AddRange(defaults.Select(d => new Procedure
            {
                Name = d.Name,
                DefaultPrice = d.Price,
                IsActive = true,
                SortOrder = order++
            }));

            await db.SaveChangesAsync();
        }
    }

    /// <summary>Demo patients and records for local development only.</summary>
    public static async Task SeedDemoDataAsync(ApplicationDbContext db)
    {
        if (await db.Patients.AnyAsync())
        {
            return;
        }

        var today = DateTime.Today;
        var now = DateTime.UtcNow;

        var patients = new[]
        {
            NewPatient("Ayşe Yılmaz", "10000000146", "0532 111 22 33", "ayse.yilmaz@example.com", new DateOnly(1985, 4, 12), today.AddDays(-40), "Sabah randevularını tercih ediyor.", "Penisilin alerjisi"),
            NewPatient("Mehmet Demir", "10000000214", "0533 222 33 44", "mehmet.demir@example.com", new DateOnly(1978, 9, 3), today.AddDays(-15), null, "Lateks alerjisi, hipertansiyon"),
            NewPatient("Zeynep Kaya", "10000000382", "0534 333 44 55", null, new DateOnly(1992, 6, 25), today.AddDays(-3), "Ortodonti tedavisi devam ediyor.", null),
            NewPatient("Ali Çelik", "10000000450", "0535 444 55 66", "ali.celik@example.com", new DateOnly(2014, 2, 8), today.AddDays(-1), "Çocuk hasta, veli: Fatma Çelik", null)
        };

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        db.Appointments.AddRange(
            new Appointment { PatientId = patients[0].Id, AppointmentDate = today.AddHours(9), DurationMinutes = 30, Purpose = "Kontrol", Status = AppointmentStatus.Scheduled },
            new Appointment { PatientId = patients[1].Id, AppointmentDate = today.AddHours(10), DurationMinutes = 60, Purpose = "Diş Çekimi", Status = AppointmentStatus.Scheduled, SelectedTeethData = "38" },
            new Appointment { PatientId = patients[3].Id, AppointmentDate = today.AddHours(14), DurationMinutes = 30, Purpose = "Flor Uygulaması", Status = AppointmentStatus.Scheduled },
            new Appointment { PatientId = patients[2].Id, AppointmentDate = today.AddDays(1).AddHours(11), DurationMinutes = 45, Purpose = "Ortodonti Kontrolü", Status = AppointmentStatus.Scheduled },
            new Appointment { PatientId = patients[0].Id, AppointmentDate = today.AddDays(3).AddHours(15).AddMinutes(30), DurationMinutes = 60, Purpose = "Dolgu", Status = AppointmentStatus.Scheduled, SelectedTeethData = "16,17" },
            new Appointment { PatientId = patients[0].Id, AppointmentDate = today.AddDays(-7).AddHours(9), DurationMinutes = 60, Purpose = "Dolgu", Status = AppointmentStatus.Completed, Notes = "Üst sol molar dolgu yapıldı.", SelectedTeethData = "26" },
            new Appointment { PatientId = patients[1].Id, AppointmentDate = today.AddDays(-2).AddHours(16), DurationMinutes = 30, Purpose = "Muayene", Status = AppointmentStatus.NoShow });

        db.PreviousOperations.Add(new PreviousOperation
        {
            PatientId = patients[0].Id,
            Date = DateOnly.FromDateTime(today.AddDays(-7)),
            Title = "Kompozit dolgu",
            Diagnosis = "26 numaralı dişte derin çürük",
            Procedures = "Lokal anestezi, çürük temizliği, kompozit dolgu",
            Prescriptions = "İbuprofen 400 mg, gerektiğinde",
            Notes = "Hasta işlemi sorunsuz tolere etti.",
            SelectedTeethData = "26"
        });

        var procedures = await db.Procedures.ToListAsync();
        Procedure P(string name) => procedures.First(p => p.Name.StartsWith(name, StringComparison.Ordinal));

        var invoice1 = new Invoice
        {
            PatientId = patients[0].Id,
            InvoiceDate = DateOnly.FromDateTime(today.AddDays(-7)),
            DueDate = DateOnly.FromDateTime(today.AddDays(23)),
            Status = InvoiceStatus.Issued,
            Notes = "Dolgu tedavisi"
        };
        invoice1.Items.Add(new InvoiceItem { Description = P("Muayene").Name, ProcedureId = P("Muayene").Id, Quantity = 1, UnitPrice = P("Muayene").DefaultPrice, SortOrder = 0 });
        invoice1.Items.Add(new InvoiceItem { Description = P("Kompozit Dolgu").Name, ProcedureId = P("Kompozit Dolgu").Id, ToothNumbers = "26", Quantity = 1, UnitPrice = P("Kompozit Dolgu").DefaultPrice, SortOrder = 1 });
        invoice1.TotalAmount = invoice1.Items.Sum(i => i.LineTotal);

        var invoice2 = new Invoice
        {
            PatientId = patients[1].Id,
            InvoiceDate = DateOnly.FromDateTime(today.AddDays(-14)),
            DueDate = DateOnly.FromDateTime(today.AddDays(-1)),
            Status = InvoiceStatus.Paid,
            Notes = "Muayene ve röntgen"
        };
        invoice2.Items.Add(new InvoiceItem { Description = P("Muayene").Name, ProcedureId = P("Muayene").Id, Quantity = 1, UnitPrice = P("Muayene").DefaultPrice, SortOrder = 0 });
        invoice2.Items.Add(new InvoiceItem { Description = P("Panoramik Röntgen").Name, ProcedureId = P("Panoramik Röntgen").Id, Quantity = 1, UnitPrice = P("Panoramik Röntgen").DefaultPrice, SortOrder = 1 });
        invoice2.TotalAmount = invoice2.Items.Sum(i => i.LineTotal);
        invoice2.Payments.Add(new Payment
        {
            PaymentDate = DateOnly.FromDateTime(today.AddDays(-10)),
            SettledDate = DateOnly.FromDateTime(today.AddDays(-10)),
            Amount = invoice2.TotalAmount,
            PaymentMethod = PaymentMethod.CreditCard,
            IsSettled = true
        });

        var invoice3 = new Invoice
        {
            PatientId = patients[2].Id,
            InvoiceDate = DateOnly.FromDateTime(today.AddDays(-30)),
            DueDate = DateOnly.FromDateTime(today.AddDays(150)),
            Status = InvoiceStatus.PartiallyPaid,
            Notes = "İmplant, 6 taksit"
        };
        invoice3.Items.Add(new InvoiceItem { Description = P("İmplant").Name, ProcedureId = P("İmplant").Id, ToothNumbers = "36", Quantity = 1, UnitPrice = P("İmplant").DefaultPrice, SortOrder = 0 });
        invoice3.TotalAmount = invoice3.Items.Sum(i => i.LineTotal);
        var plan = BillingRules.BuildInstallmentPlan(invoice3.TotalAmount, DateOnly.FromDateTime(today.AddDays(-30)), 6, 1);
        foreach (var installment in plan)
        {
            if (installment.InstallmentNumber == 1)
            {
                installment.IsSettled = true;
                installment.SettledDate = installment.PaymentDate;
                installment.PaymentMethod = PaymentMethod.Cash;
            }
            invoice3.Payments.Add(installment);
        }

        db.Invoices.AddRange(invoice1, invoice2, invoice3);
        await db.SaveChangesAsync();
    }

    private static Patient NewPatient(string name, string tckn, string? phone, string? email, DateOnly birth, DateTime arrival, string? notes, string? alerts)
    {
        var patient = new Patient
        {
            FullName = name,
            Tckn = tckn,
            Phone = phone,
            Email = email,
            BirthDate = birth,
            ArrivalDate = DateOnly.FromDateTime(arrival),
            Notes = notes,
            MedicalAlerts = alerts
        };
        patient.SearchIndex = SearchNormalizer.BuildPatientIndex(patient);
        return patient;
    }
}
