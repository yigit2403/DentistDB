using System.Text.Json;
using DentistDB.Extensions;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DentistDB.Services;

/// <summary>
/// Turns tracked entity changes into human-readable audit rows. Runs inside SaveChanges via
/// <see cref="AuditSaveChangesInterceptor"/>, so every controller gets auditing for free.
/// </summary>
public static class AuditService
{
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal)
    {
        "UpdatedAt", "CreatedAt", "SearchIndex", "PhotoData", "AnamnesisUpdatedAt"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Builds audit rows for every added/modified/deleted entity, paired with its source entry.</summary>
    public static List<(AuditEntry Entry, EntityEntry Source)> BuildEntries(IEnumerable<EntityEntry> entries, string account)
    {
        var result = new List<(AuditEntry, EntityEntry)>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditEntry)
            {
                continue;
            }

            var built = Build(entry, account, now);
            if (built != null)
            {
                result.Add((built, entry));
            }
        }

        return result;
    }

    /// <summary>Added entities get their database id only after save; patch them in.</summary>
    public static void FillGeneratedIds(IEnumerable<(AuditEntry Entry, EntityEntry Source)> pending)
    {
        foreach (var (entry, source) in pending)
        {
            if (entry.EntityId is null && TryGetIntId(source, out var id))
            {
                entry.EntityId = id;
                entry.Summary = entry.Summary.Replace("#?", $"#{id}");
            }

            if (entry.PatientId is null && source.Entity is Patient patient)
            {
                entry.PatientId = patient.Id;
            }
        }
    }

    public static AuditEntry LoginEvent(string account, bool success, string clientKey) => new()
    {
        Account = account,
        Action = success ? "Giriş" : "HatalıGiriş",
        EntityType = "Oturum",
        Summary = success ? $"{Label(account)} hesabıyla giriş yapıldı ({clientKey})" : $"{Label(account)} hesabı için hatalı PIN denemesi ({clientKey})"
    };

    public static AuditEntry LogoutEvent(string account) => new()
    {
        Account = account,
        Action = "Çıkış",
        EntityType = "Oturum",
        Summary = $"{Label(account)} hesabından çıkıldı"
    };

    public static string Label(string? account) => account switch
    {
        "admin" => "Yönetici",
        "worker" => "Çalışan",
        "system" => "Sistem",
        _ => account ?? "?"
    };

    private static AuditEntry? Build(EntityEntry entry, string account, DateTime now)
    {
        var entity = entry.Entity;
        var typeName = entity.GetType().Name;

        // PIN hashes and audit rows must never be echoed into the log.
        if (entity is AppSetting setting)
        {
            if (setting.Key.StartsWith("Pin:", StringComparison.Ordinal))
            {
                return new AuditEntry { At = now, Account = account, Action = "Güncellendi", EntityType = "Ayar", Summary = $"PIN değiştirildi: {(setting.Key == AppSetting.AdminPinHash ? "Yönetici" : "Çalışan")}" };
            }

            if (setting.Key.StartsWith("Backup:Last", StringComparison.Ordinal))
            {
                return null;
            }

            return new AuditEntry { At = now, Account = account, Action = "Güncellendi", EntityType = "Ayar", Summary = $"Ayar güncellendi: {setting.Key}" };
        }

        var action = entry.State switch
        {
            EntityState.Added => "Oluşturuldu",
            EntityState.Deleted => "Silindi",
            _ => "Güncellendi"
        };

        var changes = entry.State == EntityState.Modified
            ? entry.Properties
                .Where(p => p.IsModified && !IgnoredProperties.Contains(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue))
                .Select(p => new { field = p.Metadata.Name, from = Trim(p.OriginalValue), to = Trim(p.CurrentValue) })
                .ToList()
            : null;

        if (entry.State == EntityState.Modified && changes!.Count == 0)
        {
            return null;
        }

        var (entityLabel, subject, patientId) = Describe(entry);
        var idText = TryGetIntId(entry, out var id) && id > 0 ? $"#{id}" : "#?";
        var changedFields = changes is { Count: > 0 } ? $" ({string.Join(", ", changes.Select(c => FieldLabel(c.field)))})" : string.Empty;

        return new AuditEntry
        {
            At = now,
            Account = account,
            Action = action,
            EntityType = typeName,
            EntityId = id > 0 ? id : null,
            PatientId = patientId,
            Summary = $"{entityLabel} {action.ToLowerInvariant()}: {subject} {idText}{changedFields}",
            Details = changes is { Count: > 0 } ? Truncate(JsonSerializer.Serialize(changes, JsonOptions), 4000) : null
        };
    }

    private static (string Label, string Subject, int? PatientId) Describe(EntityEntry entry)
    {
        return entry.Entity switch
        {
            Patient p => ("Hasta", p.FullName, p.Id > 0 ? p.Id : null),
            Appointment a => ("Randevu", $"{a.AppointmentDate:dd.MM.yyyy HH:mm} {a.Purpose}", a.PatientId),
            PreviousOperation o => ("Tedavi kaydı", $"{o.Date:dd.MM.yyyy} {o.Title}", o.PatientId),
            Scan s => ("Görüntü", $"{s.FileName}", s.PatientId),
            Invoice i => ("Fatura", $"{i.InvoiceDate:dd.MM.yyyy} {i.TotalAmount:N2} ₺ [{i.Status}]", i.PatientId),
            InvoiceItem ii => ("Fatura kalemi", $"{ii.Description} x{ii.Quantity}", null),
            Payment pay => ("Ödeme", $"{pay.Amount:N2} ₺ {(pay.IsPlanned ? $"{pay.InstallmentNumber}. taksit" : pay.PaymentMethod.ToString())}{(pay.IsSettled ? " (tahsil)" : "")}", null),
            Procedure pr => ("Fiyat listesi", $"{pr.Name} {pr.DefaultPrice:N2} ₺", null),
            ToothStatus t => ("Diş durumu", $"{t.ToothNumber} → {t.Condition}", t.PatientId),
            TreatmentPlanItem tp => ("Tedavi planı", $"{tp.Description} [{tp.Status}]", tp.PatientId),
            ConsentRecord c => ("Onam", $"{c.Type} {c.SignedOn:dd.MM.yyyy}", c.PatientId),
            _ => (entry.Entity.GetType().Name, string.Empty, null)
        };
    }

    private static string FieldLabel(string field) => field switch
    {
        "FullName" => "Ad Soyad",
        "Phone" => "Telefon",
        "Tckn" => "TCKN",
        "Email" => "E-posta",
        "BirthDate" => "Doğum tarihi",
        "Address" => "Adres",
        "Notes" => "Notlar",
        "MedicalAlerts" => "Tıbbi uyarılar",
        "IsArchived" => "Arşiv",
        "AppointmentDate" => "Tarih",
        "DurationMinutes" => "Süre",
        "Purpose" => "Neden",
        "Status" => "Durum",
        "SelectedTeethData" => "Dişler",
        "TotalAmount" => "Toplam",
        "DueDate" => "Vade",
        "Amount" => "Tutar",
        "IsSettled" => "Tahsil",
        "SettledDate" => "Tahsil tarihi",
        "PaymentMethod" => "Ödeme yöntemi",
        "DefaultPrice" => "Fiyat",
        "IsActive" => "Aktif",
        "Condition" => "Durum",
        "Title" => "Başlık",
        "Diagnosis" => "Tanı",
        "Procedures" => "İşlemler",
        "Prescriptions" => "Reçete",
        _ => field
    };

    private static bool TryGetIntId(EntityEntry entry, out int id)
    {
        id = 0;
        var key = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
        if (key == null) return false;
        var value = entry.Property(key.Name).CurrentValue;
        if (value is int i) { id = i; return true; }
        if (value is long l) { id = (int)l; return true; }
        return false;
    }

    private static string? Trim(object? value)
    {
        if (value is null) return null;
        var text = value switch
        {
            DateTime dt => dt.ToString("dd.MM.yyyy HH:mm"),
            DateOnly d => d.ToString("dd.MM.yyyy"),
            decimal m => m.ToString("N2"),
            byte[] => "[dosya]",
            _ => value.ToString()
        };
        return Truncate(text, 120);
    }

    private static string? Truncate(string? text, int max) => text is null || text.Length <= max ? text : text[..max] + "…";
}

/// <summary>Writes audit rows in the same transaction as the business change.</summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentAccountAccessor _account;
    private readonly List<(AuditEntry Entry, EntityEntry Source)> _pendingAdds = new();

    public AuditSaveChangesInterceptor(ICurrentAccountAccessor account)
    {
        _account = account;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FlushAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    private void Collect(DbContext? context)
    {
        if (context is null || _pendingAdds.Count > 0)
        {
            return;
        }

        var account = _account.AccountKey;
        _pendingAdds.AddRange(AuditService.BuildEntries(context.ChangeTracker.Entries().ToList(), account));
    }

    private async Task FlushAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null || _pendingAdds.Count == 0)
        {
            return;
        }

        var batch = _pendingAdds.ToList();
        _pendingAdds.Clear();

        AuditService.FillGeneratedIds(batch);
        context.Set<AuditEntry>().AddRange(batch.Select(b => b.Entry));

        // Second save only contains audit rows; Collect() skips them so this cannot recurse.
        await context.SaveChangesAsync(ct);
    }
}
