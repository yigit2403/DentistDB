using Microsoft.EntityFrameworkCore;
using DentistDB.Models;

namespace DentistDB.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PreviousOperation> PreviousOperations => Set<PreviousOperation>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<ToothStatus> TeethStatus => Set<ToothStatus>();
    public DbSet<TreatmentPlanItem> TreatmentPlanItems => Set<TreatmentPlanItem>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.FullName);
            e.HasIndex(p => p.Tckn).IsUnique();
            e.HasIndex(p => p.LegacyKey).IsUnique();
            e.HasIndex(p => p.SearchIndex);
            e.HasIndex(p => p.ArrivalDate);
        });

        builder.Entity<Appointment>(e =>
        {
            e.HasIndex(a => a.AppointmentDate);
            e.HasIndex(a => new { a.PatientId, a.AppointmentDate });
            e.HasOne(a => a.Patient)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PreviousOperation>(e =>
        {
            e.HasIndex(o => new { o.PatientId, o.Date });
            e.HasIndex(o => o.SearchIndex);
            e.HasOne(o => o.Patient)
             .WithMany(p => p.PreviousOperations)
             .HasForeignKey(o => o.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Scan>(e =>
        {
            e.HasIndex(s => new { s.PatientId, s.ScanDate });
            e.HasOne(s => s.Patient)
             .WithMany(p => p.Scans)
             .HasForeignKey(s => s.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.InvoiceDate);
            e.HasIndex(i => i.Status);
            e.HasOne(i => i.Patient)
             .WithMany(p => p.Invoices)
             .HasForeignKey(i => i.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InvoiceItem>(e =>
        {
            e.HasIndex(i => i.InvoiceId);
            e.HasOne(i => i.Invoice)
             .WithMany(inv => inv.Items)
             .HasForeignKey(i => i.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Procedure)
             .WithMany()
             .HasForeignKey(i => i.ProcedureId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasIndex(p => new { p.InvoiceId, p.PaymentDate, p.IsPlanned });
            e.HasIndex(p => new { p.IsPlanned, p.IsSettled, p.PaymentDate });
            e.HasOne(p => p.Invoice)
             .WithMany(i => i.Payments)
             .HasForeignKey(p => p.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Procedure>(e =>
        {
            e.HasIndex(p => p.Name);
            e.HasIndex(p => new { p.IsActive, p.SortOrder });
        });

        builder.Entity<ToothStatus>(e =>
        {
            e.HasIndex(t => new { t.PatientId, t.ToothNumber }).IsUnique();
            e.HasOne(t => t.Patient)
             .WithMany(p => p.Teeth)
             .HasForeignKey(t => t.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TreatmentPlanItem>(e =>
        {
            e.HasIndex(t => new { t.PatientId, t.Status, t.SortOrder });
            e.HasOne(t => t.Patient)
             .WithMany(p => p.TreatmentPlan)
             .HasForeignKey(t => t.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Procedure).WithMany().HasForeignKey(t => t.ProcedureId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Appointment).WithMany().HasForeignKey(t => t.AppointmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.PreviousOperation).WithMany().HasForeignKey(t => t.PreviousOperationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Invoice).WithMany().HasForeignKey(t => t.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ConsentRecord>(e =>
        {
            e.HasIndex(c => new { c.PatientId, c.Type });
            e.HasOne(c => c.Patient)
             .WithMany(p => p.Consents)
             .HasForeignKey(c => c.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditEntry>(e =>
        {
            e.HasIndex(a => a.At);
            e.HasIndex(a => a.PatientId);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
        });
    }
}
