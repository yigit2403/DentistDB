using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DentistDB.Models;

namespace DentistDB.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TreatmentRecord> TreatmentRecords => Set<TreatmentRecord>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.FullName);
        });

        builder.Entity<Appointment>(e =>
        {
            e.HasIndex(a => a.AppointmentDate);
            e.HasOne(a => a.Patient)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TreatmentRecord>(e =>
        {
            e.HasOne(t => t.Patient)
             .WithMany(p => p.TreatmentRecords)
             .HasForeignKey(t => t.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Scan>(e =>
        {
            e.HasOne(s => s.Patient)
             .WithMany(p => p.Scans)
             .HasForeignKey(s => s.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasOne(i => i.Patient)
             .WithMany(p => p.Invoices)
             .HasForeignKey(i => i.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasOne(p => p.Invoice)
             .WithMany(i => i.Payments)
             .HasForeignKey(p => p.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
