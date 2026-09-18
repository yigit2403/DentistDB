using System.Globalization;
using System.Text.Json;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.Services;
using Microsoft.EntityFrameworkCore;

// ---------------------------------------------------------------------------
// One-time importer for the old Microsoft Access dental program.
// Reads the JSON produced by tools/legacy-import/extract.ps1 and upserts the
// patients into a DentistDB SQLite database, reusing the app's EF model so the
// schema and search index stay identical to hand-entered records.
//
//   dotnet run --project tools/LegacyImport -- --json patients.json --db DentistDB.db [--dry-run]
//
// Idempotent: a patient is matched on LegacyKey (the old KisiNumara), so a
// second run updates rather than duplicates. TCKN is unique in the schema, so a
// value already taken by another patient is dropped to null and noted; the
// patient is still imported and flagged (NeedsTckn) for follow-up.
// ---------------------------------------------------------------------------

string? jsonPath = null, dbPath = null;
var dryRun = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--json": jsonPath = args[++i]; break;
        case "--db": dbPath = args[++i]; break;
        case "--dry-run": dryRun = true; break;
        default: Console.Error.WriteLine($"Bilinmeyen argüman: {args[i]}"); return 2;
    }
}

if (jsonPath is null || dbPath is null)
{
    Console.Error.WriteLine("Kullanım: --json <patients.json> --db <DentistDB.db> [--dry-run]");
    return 2;
}
if (!File.Exists(jsonPath))
{
    Console.Error.WriteLine($"JSON bulunamadı: {jsonPath}");
    return 2;
}

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var payload = JsonSerializer.Deserialize<Payload>(File.ReadAllText(jsonPath), options);
var incoming = payload?.Patients ?? new List<LegacyPatient>();
Console.WriteLine($"{incoming.Count} kayıt okundu: {jsonPath}");

var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

await using var db = new ApplicationDbContext(dbOptions);
await db.Database.MigrateAsync();

// Existing state: patients already imported (by LegacyKey) and which TCKN belongs to whom.
var existingByLegacy = await db.Patients
    .Where(p => p.LegacyKey != null)
    .ToDictionaryAsync(p => p.LegacyKey!.Value);
var tcknOwner = await db.Patients
    .Where(p => p.Tckn != null && p.Tckn != "")
    .ToDictionaryAsync(p => p.Tckn!, p => p.LegacyKey ?? -p.Id); // own key, or a negative Id sentinel for app-created patients

int created = 0, updated = 0, tcknKept = 0, tcknDropped = 0, tcknBlank = 0, skipped = 0;

foreach (var row in incoming)
{
    if (string.IsNullOrWhiteSpace(row.FullName)) { skipped++; continue; }

    // Resolve the TCKN against the uniqueness rule.
    string? tckn = string.IsNullOrWhiteSpace(row.Tckn) ? null : row.Tckn.Trim();
    var dupNote = false;
    if (tckn is not null)
    {
        if (tcknOwner.TryGetValue(tckn, out var owner) && owner != row.LegacyKey)
        {
            tckn = null; dupNote = true; tcknDropped++;
        }
        else
        {
            tcknOwner[tckn] = row.LegacyKey; tcknKept++;
        }
    }
    if (tckn is null && !dupNote) tcknBlank++;

    var notes = row.Notes;
    if (dupNote)
    {
        notes = string.IsNullOrWhiteSpace(notes)
            ? "Aynı TCKN başka bir kayıtta olduğu için boş bırakıldı."
            : notes + " | Aynı TCKN başka bir kayıtta olduğu için boş bırakıldı.";
        if (notes.Length > 2000) notes = notes[..2000];
    }

    if (!existingByLegacy.TryGetValue(row.LegacyKey, out var patient))
    {
        patient = new Patient { LegacyKey = row.LegacyKey, CreatedAt = DateTime.UtcNow };
        db.Patients.Add(patient);
        created++;
    }
    else
    {
        updated++;
    }

    patient.FullName = row.FullName.Trim();
    patient.Tckn = tckn;
    patient.Phone = row.Phone;
    patient.BirthDate = ParseDate(row.BirthDate);
    patient.ArrivalDate = ParseDate(row.ArrivalDate);
    patient.Address = row.Address;
    patient.Notes = notes;
    patient.IsImported = true;
    patient.SearchIndex = SearchNormalizer.BuildPatientIndex(patient);
    patient.UpdatedAt = DateTime.UtcNow;
}

Console.WriteLine($"  yeni:            {created}");
Console.WriteLine($"  güncellenen:     {updated}");
Console.WriteLine($"  atlanan (adsız): {skipped}");
Console.WriteLine($"  TCKN korunan:    {tcknKept}");
Console.WriteLine($"  TCKN boş:        {tcknBlank}");
Console.WriteLine($"  TCKN çakışması:  {tcknDropped}");

if (dryRun)
{
    Console.WriteLine("--dry-run: değişiklik kaydedilmedi.");
    return 0;
}

var written = await db.SaveChangesAsync();
Console.WriteLine($"Kaydedildi ({written} değişiklik). Toplam hasta: {await db.Patients.CountAsync()}");
return 0;

static DateOnly? ParseDate(string? s) =>
    DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

internal sealed record Payload(List<LegacyPatient> Patients);
internal sealed record LegacyPatient(
    int LegacyKey,
    string FullName,
    string? Tckn,
    string? Phone,
    string? BirthDate,
    string? ArrivalDate,
    string? Address,
    string? Notes);
