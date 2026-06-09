using DentistDB.Models;

namespace DentistDB.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        // Seed patients if empty
        if (!db.Patients.Any())
        {
            var patients = new[]
            {
                new Patient
                {
                    FullName = "Alice Johnson",
                    Tckn = "11111111110",
                    Phone = "555-0101",
                    Email = "alice@example.com",
                    BirthDate = new DateOnly(1985, 4, 12),
                    ArrivalDate = DateOnly.FromDateTime(DateTime.Today),
                    Address = "123 Maple St, Springfield",
                    Notes = "Prefers morning appointments.",
                    MedicalAlerts = "Penicillin allergy",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Patient
                {
                    FullName = "Bob Martinez",
                    Tckn = "22222222220",
                    Phone = "555-0102",
                    Email = "bob@example.com",
                    BirthDate = new DateOnly(1978, 9, 3),
                    ArrivalDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
                    Address = "456 Oak Ave, Springfield",
                    MedicalAlerts = "Latex allergy",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Patient
                {
                    FullName = "Carol White",
                    Tckn = "33333333330",
                    Phone = "555-0103",
                    Email = "carol@example.com",
                    BirthDate = new DateOnly(1992, 6, 25),
                    ArrivalDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
                    Address = "789 Pine Rd, Springfield",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            db.Patients.AddRange(patients);
            await db.SaveChangesAsync();

            // Seed appointments
            var today = DateTime.Today;
            var appointments = new[]
            {
                new Appointment
                {
                    PatientId = patients[0].Id,
                    AppointmentDate = today.AddHours(9),
                    Purpose = "Routine checkup",
                    Status = AppointmentStatus.Scheduled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Appointment
                {
                    PatientId = patients[1].Id,
                    AppointmentDate = today.AddHours(11),
                    Purpose = "Tooth extraction",
                    Status = AppointmentStatus.Scheduled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Appointment
                {
                    PatientId = patients[2].Id,
                    AppointmentDate = today.AddDays(2).AddHours(10),
                    Purpose = "Teeth cleaning",
                    Status = AppointmentStatus.Scheduled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Appointment
                {
                    PatientId = patients[0].Id,
                    AppointmentDate = today.AddDays(-7).AddHours(9),
                    Purpose = "Filling",
                    Status = AppointmentStatus.Completed,
                    Notes = "Upper left molar filled.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            db.Appointments.AddRange(appointments);

            var previousOperations = new[]
            {
                new PreviousOperation
                {
                    PatientId = patients[0].Id,
                    Date = DateOnly.FromDateTime(today.AddDays(-7)),
                    Title = "Kompozit dolgu",
                    Diagnosis = "Üst sol molarda çürük",
                    Procedures = "Kompozit dolgu uygulandı",
                    Prescriptions = "İbuprofen 400 mg gerektiğinde",
                    Notes = "Hasta işlemi sorunsuz tolere etti.",
                    SelectedTeethData = "26",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            db.PreviousOperations.AddRange(previousOperations);

            // Seed invoices
            var invoices = new[]
            {
                new Invoice
                {
                    PatientId = patients[0].Id,
                    InvoiceDate = DateOnly.FromDateTime(today.AddDays(-7)),
                    DueDate = DateOnly.FromDateTime(today.AddDays(23)),
                    TotalAmount = 250.00m,
                    Status = InvoiceStatus.Issued,
                    Notes = "Composite filling procedure",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Invoice
                {
                    PatientId = patients[1].Id,
                    InvoiceDate = DateOnly.FromDateTime(today.AddDays(-14)),
                    DueDate = DateOnly.FromDateTime(today.AddDays(-1)),
                    TotalAmount = 180.00m,
                    Status = InvoiceStatus.Paid,
                    Notes = "Routine checkup + X-ray",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            db.Invoices.AddRange(invoices);
            await db.SaveChangesAsync();

            // Seed payment for paid invoice
            db.Payments.Add(new Payment
            {
                InvoiceId = invoices[1].Id,
                PaymentDate = DateOnly.FromDateTime(today.AddDays(-10)),
                Amount = 180.00m,
                PaymentMethod = PaymentMethod.Cash,
                IsPlanned = false,
                IsSettled = true,
                SettledDate = DateOnly.FromDateTime(today.AddDays(-10)),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
