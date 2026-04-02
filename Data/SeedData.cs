using DentistDB.Models;

namespace DentistDB.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        if (db.Patients.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var today = DateTime.Today;

        var patients = new[]
        {
            new Patient
            {
                FullName = "Ayşe Yılmaz",
                Tckn = "11111111110",
                Phone = "0532 111 11 10",
                Email = "ayse.yilmaz@example.com",
                BirthDate = new DateOnly(1988, 5, 14),
                Address = "Kadıköy, İstanbul",
                Notes = "Sabah saatlerini tercih ediyor.",
                MedicalAlerts = "Penisilin alerjisi",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Patient
            {
                FullName = "Mehmet Kaya",
                Tckn = "22222222220",
                Phone = "0532 222 22 20",
                Email = "mehmet.kaya@example.com",
                BirthDate = new DateOnly(1979, 11, 3),
                Address = "Çankaya, Ankara",
                Notes = "Kontrollerini aksatmaz.",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Patient
            {
                FullName = "Elif Demir",
                Tckn = "33333333330",
                Phone = "0532 333 33 30",
                Email = "elif.demir@example.com",
                BirthDate = new DateOnly(1994, 2, 21),
                Address = "Bornova, İzmir",
                MedicalAlerts = "Lateks hassasiyeti",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        var invoices = new[]
        {
            new Invoice
            {
                PatientId = patients[0].Id,
                InvoiceDate = DateOnly.FromDateTime(today.AddDays(-7)),
                DueDate = DateOnly.FromDateTime(today.AddDays(23)),
                TotalAmount = 480.00m,
                Status = InvoiceStatus.PartiallyPaid,
                Notes = "Kanal tedavisi ve kompozit dolgu",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Invoice
            {
                PatientId = patients[1].Id,
                InvoiceDate = DateOnly.FromDateTime(today.AddDays(-14)),
                DueDate = DateOnly.FromDateTime(today.AddDays(-1)),
                TotalAmount = 350.00m,
                Status = InvoiceStatus.Paid,
                Notes = "Detartraj ve panoramik film",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync();

        var appointments = new[]
        {
            new Appointment
            {
                PatientId = patients[0].Id,
                InvoiceId = invoices[0].Id,
                AppointmentDate = today.AddHours(9),
                Purpose = "Kontrol ve pansuman",
                Status = AppointmentStatus.Scheduled,
                Notes = "İşlem sonrası kontrol randevusu",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Appointment
            {
                PatientId = patients[1].Id,
                InvoiceId = invoices[1].Id,
                AppointmentDate = today.AddHours(11),
                Purpose = "Diş taşı temizliği",
                Status = AppointmentStatus.Scheduled,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Appointment
            {
                PatientId = patients[2].Id,
                AppointmentDate = today.AddDays(2).AddHours(10),
                Purpose = "Dolgu kontrolü",
                Status = AppointmentStatus.Scheduled,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Appointment
            {
                PatientId = patients[0].Id,
                InvoiceId = invoices[0].Id,
                AppointmentDate = today.AddDays(-7).AddHours(9),
                Purpose = "Kanal tedavisi",
                Status = AppointmentStatus.Completed,
                Notes = "26 numaralı dişte kanal tedavisi tamamlandı.",
                SelectedTeethData = "26",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        db.Appointments.AddRange(appointments);

        var previousOperations = new[]
        {
            new PreviousOperation
            {
                PatientId = patients[0].Id,
                InvoiceId = invoices[0].Id,
                Date = DateOnly.FromDateTime(today.AddDays(-7)),
                PriceAmount = 480.00m,
                Title = "Kanal tedavisi ve dolgu",
                Diagnosis = "26 numaralı dişte derin çürük",
                Procedures = "Kanal tedavisi yapıldı, kompozit dolgu uygulandı",
                Prescriptions = "İbuprofen 400 mg gerektiğinde kullanılacak",
                Notes = "Hasta işlemi sorunsuz tolere etti.",
                SelectedTeethData = "26",
                CreatedAt = now,
                UpdatedAt = now
            },
            new PreviousOperation
            {
                PatientId = patients[1].Id,
                InvoiceId = invoices[1].Id,
                Date = DateOnly.FromDateTime(today.AddDays(-14)),
                PriceAmount = 350.00m,
                Title = "Detartraj ve panoramik değerlendirme",
                Diagnosis = "Yaygın diş taşı birikimi",
                Procedures = "Detertraj yapıldı, panoramik film değerlendirildi",
                Notes = "Ağız hijyeni önerileri paylaşıldı.",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        db.PreviousOperations.AddRange(previousOperations);

        db.Payments.AddRange(
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(-5)),
                Amount = 120.00m,
                PaymentMethod = PaymentMethod.CreditCard,
                Notes = "Peşinat",
                IsPlanned = false,
                IsSettled = true,
                SettledDate = DateOnly.FromDateTime(today.AddDays(-5)),
                CreatedAt = now
            },
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(30)),
                Amount = 180.00m,
                PaymentMethod = PaymentMethod.Other,
                Notes = "1. taksit",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = 1,
                CreatedAt = now
            },
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(60)),
                Amount = 180.00m,
                PaymentMethod = PaymentMethod.Other,
                Notes = "2. taksit",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = 2,
                CreatedAt = now
            },
            new Payment
            {
                InvoiceId = invoices[1].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(-10)),
                Amount = 350.00m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "Tam ödeme",
                IsPlanned = false,
                IsSettled = true,
                SettledDate = DateOnly.FromDateTime(today.AddDays(-10)),
                CreatedAt = now
            });

        await db.SaveChangesAsync();
    }
}
