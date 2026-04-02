using DentistDB.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentistDB.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PreviousOperation> PreviousOperations => Set<PreviousOperation>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigurePatients(builder.Entity<Patient>());
        ConfigureAppointments(builder.Entity<Appointment>());
        ConfigurePreviousOperations(builder.Entity<PreviousOperation>());
        ConfigureScans(builder.Entity<Scan>());
        ConfigureInvoices(builder.Entity<Invoice>());
        ConfigurePayments(builder.Entity<Payment>());
    }

    private static void ConfigurePatients(EntityTypeBuilder<Patient> entity)
    {
        entity.HasIndex(patient => patient.FullName);
    }

    private static void ConfigureAppointments(EntityTypeBuilder<Appointment> entity)
    {
        entity.HasIndex(appointment => appointment.AppointmentDate);
        entity.HasIndex(appointment => appointment.InvoiceId);
        entity.HasOne(appointment => appointment.Patient)
            .WithMany(patient => patient.Appointments)
            .HasForeignKey(appointment => appointment.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(appointment => appointment.Invoice)
            .WithMany(invoice => invoice.Appointments)
            .HasForeignKey(appointment => appointment.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigurePreviousOperations(EntityTypeBuilder<PreviousOperation> entity)
    {
        entity.HasIndex(operation => new { operation.PatientId, operation.Date });
        entity.HasIndex(operation => operation.InvoiceId);

        entity.HasOne(operation => operation.Patient)
            .WithMany(patient => patient.PreviousOperations)
            .HasForeignKey(operation => operation.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(operation => operation.Invoice)
            .WithMany(invoice => invoice.PreviousOperations)
            .HasForeignKey(operation => operation.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureScans(EntityTypeBuilder<Scan> entity)
    {
        entity.HasOne(scan => scan.Patient)
            .WithMany(patient => patient.Scans)
            .HasForeignKey(scan => scan.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInvoices(EntityTypeBuilder<Invoice> entity)
    {
        entity.HasOne(invoice => invoice.Patient)
            .WithMany(patient => patient.Invoices)
            .HasForeignKey(invoice => invoice.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePayments(EntityTypeBuilder<Payment> entity)
    {
        entity.HasIndex(payment => new { payment.InvoiceId, payment.PaymentDate, payment.IsPlanned });
        entity.HasOne(payment => payment.Invoice)
            .WithMany(invoice => invoice.Payments)
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
