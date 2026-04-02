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
                Notes = "Taksitli tedavi planı devam ediyor.",
                MedicalAlerts = "Penisilin alerjisi",
                CreatedAt = now.AddDays(-21),
                UpdatedAt = now.AddDays(-1)
            },
            new Patient
            {
                FullName = "Mehmet Kaya",
                Tckn = "22222222220",
                Phone = "0532 222 22 20",
                Email = "mehmet.kaya@example.com",
                BirthDate = new DateOnly(1979, 11, 3),
                Address = "Çankaya, Ankara",
                Notes = "Peşin ödeme yapmayı tercih ediyor.",
                CreatedAt = now.AddDays(-18),
                UpdatedAt = now.AddDays(-2)
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
                Notes = "Henüz finans hesabı oluşturulmadı.",
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-1)
            },
            new Patient
            {
                FullName = "Can Aydın",
                Tckn = "44444444440",
                Phone = "0532 444 44 40",
                Email = "can.aydin@example.com",
                BirthDate = new DateOnly(1985, 8, 9),
                Address = "Beşiktaş, İstanbul",
                Notes = "Açık hesap var, ancak taksit planı başlatılmadı.",
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-3)
            }
        };

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        var invoices = new[]
        {
            new Invoice
            {
                PatientId = patients[0].Id,
                InvoiceDate = DateOnly.FromDateTime(today.AddDays(-14)),
                DueDate = DateOnly.FromDateTime(today.AddDays(45)),
                TotalAmount = 1200.00m,
                Status = InvoiceStatus.PartiallyPaid,
                Notes = "Kanal tedavisi, dolgu ve takip seansları",
                CreatedAt = now.AddDays(-14),
                UpdatedAt = now.AddDays(-1)
            },
            new Invoice
            {
                PatientId = patients[1].Id,
                InvoiceDate = DateOnly.FromDateTime(today.AddDays(-8)),
                DueDate = DateOnly.FromDateTime(today.AddDays(-1)),
                TotalAmount = 650.00m,
                Status = InvoiceStatus.Paid,
                Notes = "Detartraj ve tek seans dolgu",
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-5)
            },
            new Invoice
            {
                PatientId = patients[3].Id,
                InvoiceDate = DateOnly.FromDateTime(today.AddDays(-4)),
                DueDate = DateOnly.FromDateTime(today.AddDays(20)),
                TotalAmount = 900.00m,
                Status = InvoiceStatus.Issued,
                Notes = "Açık hesap, tahsilat bekleniyor",
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-2)
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
                Notes = "Taksit planı devam ederken kontrol randevusu.",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new Appointment
            {
                PatientId = patients[1].Id,
                InvoiceId = invoices[1].Id,
                AppointmentDate = today.AddDays(1).AddHours(11),
                Purpose = "Parlatma ve kontrol",
                Status = AppointmentStatus.Scheduled,
                Notes = "Önceki seansın son kontrolü.",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2)
            },
            new Appointment
            {
                PatientId = patients[2].Id,
                AppointmentDate = today.AddDays(2).AddHours(10),
                Purpose = "Yeni hasta muayenesi",
                Status = AppointmentStatus.Scheduled,
                Notes = "Henüz finans hesabı olmayan hasta örneği.",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new Appointment
            {
                PatientId = patients[3].Id,
                InvoiceId = invoices[2].Id,
                AppointmentDate = today.AddDays(3).AddHours(15),
                Purpose = "Kuron ölçüsü",
                Status = AppointmentStatus.Scheduled,
                Notes = "Açık hesap üzerine yeni borç eklenebilecek hasta örneği.",
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-1)
            },
            new Appointment
            {
                PatientId = patients[0].Id,
                InvoiceId = invoices[0].Id,
                AppointmentDate = today.AddDays(-14).AddHours(9),
                Purpose = "Kanal tedavisi",
                Status = AppointmentStatus.Completed,
                Notes = "26 numaralı dişte kanal tedavisi tamamlandı.",
                SelectedTeethData = "26",
                CreatedAt = now.AddDays(-14),
                UpdatedAt = now.AddDays(-14)
            }
        };

        db.Appointments.AddRange(appointments);

        var previousOperations = new[]
        {
            new PreviousOperation
            {
                PatientId = patients[0].Id,
                InvoiceId = invoices[0].Id,
                Date = DateOnly.FromDateTime(today.AddDays(-14)),
                PriceAmount = 1200.00m,
                Title = "Kanal tedavisi ve dolgu",
                Diagnosis = "26 numaralı dişte derin çürük",
                Procedures = "Kanal tedavisi yapıldı, kompozit dolgu uygulandı",
                Prescriptions = "İbuprofen 400 mg gerektiğinde kullanılacak",
                Notes = "Hasta işlemi sorunsuz tolere etti.",
                SelectedTeethData = "26",
                CreatedAt = now.AddDays(-14),
                UpdatedAt = now.AddDays(-14)
            },
            new PreviousOperation
            {
                PatientId = patients[1].Id,
                InvoiceId = invoices[1].Id,
                Date = DateOnly.FromDateTime(today.AddDays(-8)),
                PriceAmount = 650.00m,
                Title = "Detartraj ve dolgu",
                Diagnosis = "Yaygın diş taşı birikimi ve yüzeysel çürük",
                Procedures = "Detartraj yapıldı, tek seans dolgu tamamlandı",
                Notes = "Tahsilat aynı gün tamamen yapıldı.",
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-8)
            },
            new PreviousOperation
            {
                PatientId = patients[3].Id,
                InvoiceId = invoices[2].Id,
                Date = DateOnly.FromDateTime(today.AddDays(-4)),
                PriceAmount = 900.00m,
                Title = "Kuron hazırlığı",
                Diagnosis = "Kuron ihtiyacı",
                Procedures = "Ölçü hazırlığı ve geçici uygulama",
                Notes = "Açık hesap olarak takibe alındı.",
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-4)
            }
        };

        db.PreviousOperations.AddRange(previousOperations);

        db.Payments.AddRange(
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(-13)),
                Amount = 300.00m,
                PaymentMethod = PaymentMethod.CreditCard,
                Notes = "İlk peşinat",
                IsPlanned = false,
                IsSettled = true,
                SettledDate = DateOnly.FromDateTime(today.AddDays(-13)),
                CreatedAt = now.AddDays(-13)
            },
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(15)),
                Amount = 300.00m,
                PaymentMethod = PaymentMethod.Other,
                Notes = "1. planlı taksit",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = 1,
                CreatedAt = now.AddDays(-13)
            },
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(45)),
                Amount = 300.00m,
                PaymentMethod = PaymentMethod.Other,
                Notes = "2. planlı taksit",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = 2,
                CreatedAt = now.AddDays(-13)
            },
            new Payment
            {
                InvoiceId = invoices[0].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(75)),
                Amount = 300.00m,
                PaymentMethod = PaymentMethod.Other,
                Notes = "3. planlı taksit",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = 3,
                CreatedAt = now.AddDays(-13)
            },
            new Payment
            {
                InvoiceId = invoices[1].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(-8)),
                Amount = 650.00m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "Peşin ödeme",
                IsPlanned = false,
                IsSettled = true,
                SettledDate = DateOnly.FromDateTime(today.AddDays(-8)),
                CreatedAt = now.AddDays(-8)
            });

        await db.SaveChangesAsync();
    }
}
