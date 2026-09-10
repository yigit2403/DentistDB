using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

/// <summary>Planned work per patient. Items flow into appointments, treatment records and invoices.</summary>
public class TreatmentPlanController : ClinicControllerBase
{
    public TreatmentPlanController(ApplicationDbContext db) : base(db) { }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TreatmentPlanFormModel model)
    {
        var patientExists = await Db.Patients.AnyAsync(p => p.Id == model.PatientId);
        if (!patientExists) return NotFound();

        if (!ModelState.IsValid)
        {
            Error("Plan kalemi kaydedilemedi: " + string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return Back(model.PatientId);
        }

        var description = model.Description.Trim();
        var teeth = TeethSelectionSerializer.Normalize(model.ToothNumbers) ?? (string.IsNullOrWhiteSpace(model.ToothNumbers) ? null : model.ToothNumbers.Trim());

        if (model.Id > 0)
        {
            var item = await Db.TreatmentPlanItems.FirstOrDefaultAsync(t => t.Id == model.Id && t.PatientId == model.PatientId);
            if (item == null) return NotFound();

            item.ProcedureId = model.ProcedureId;
            item.Description = description;
            item.ToothNumbers = teeth;
            item.EstimatedPrice = Math.Round(model.EstimatedPrice, 2, MidpointRounding.AwayFromZero);
            item.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            Success("Plan kalemi güncellendi.");
        }
        else
        {
            var maxOrder = await Db.TreatmentPlanItems.Where(t => t.PatientId == model.PatientId).MaxAsync(t => (int?)t.SortOrder) ?? -1;
            Db.TreatmentPlanItems.Add(new TreatmentPlanItem
            {
                PatientId = model.PatientId,
                ProcedureId = model.ProcedureId,
                Description = description,
                ToothNumbers = teeth,
                EstimatedPrice = Math.Round(model.EstimatedPrice, 2, MidpointRounding.AwayFromZero),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                Status = TreatmentPlanStatus.Planned,
                SortOrder = maxOrder + 1
            });
            Success("Tedavi planına eklendi.");
        }

        await Db.SaveChangesAsync();
        return Back(model.PatientId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, TreatmentPlanStatus status)
    {
        var item = await Db.TreatmentPlanItems.FindAsync(id);
        if (item == null) return NotFound();

        if (status == TreatmentPlanStatus.Done)
        {
            return await CompleteInternalAsync(item);
        }

        item.Status = status;
        if (status == TreatmentPlanStatus.Planned)
        {
            item.AppointmentId = null;
        }
        await Db.SaveChangesAsync();
        Success(status == TreatmentPlanStatus.Cancelled ? "Plan kalemi iptal edildi." : "Plan kalemi yeniden açıldı.");
        return Back(item.PatientId);
    }

    /// <summary>Marks the item done and writes a treatment record for it.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, DateOnly? date, string? notes)
    {
        var item = await Db.TreatmentPlanItems.FindAsync(id);
        if (item == null) return NotFound();
        return await CompleteInternalAsync(item, date, notes);
    }

    private async Task<IActionResult> CompleteInternalAsync(TreatmentPlanItem item, DateOnly? date = null, string? notes = null)
    {
        if (item.Status == TreatmentPlanStatus.Done)
        {
            return Back(item.PatientId);
        }

        var operation = new PreviousOperation
        {
            PatientId = item.PatientId,
            Date = date ?? DateOnly.FromDateTime(DateTime.Today),
            Title = item.Description,
            Procedures = item.Description,
            Notes = string.IsNullOrWhiteSpace(notes) ? item.Notes : notes.Trim(),
            SelectedTeethData = TeethSelectionSerializer.Normalize(item.ToothNumbers),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Db.PreviousOperations.Add(operation);
        await Db.SaveChangesAsync();

        item.Status = TreatmentPlanStatus.Done;
        item.CompletedAt = DateTime.UtcNow;
        item.PreviousOperationId = operation.Id;
        await Db.SaveChangesAsync();

        Success($"“{item.Description}” tamamlandı ve tedavi kaydı oluşturuldu.");
        return Back(item.PatientId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await Db.TreatmentPlanItems.FindAsync(id);
        if (item == null) return NotFound();

        Db.TreatmentPlanItems.Remove(item);
        await Db.SaveChangesAsync();
        Success("Plan kalemi silindi.");
        return Back(item.PatientId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction)
    {
        var item = await Db.TreatmentPlanItems.FindAsync(id);
        if (item == null) return NotFound();

        var siblings = await Db.TreatmentPlanItems
            .Where(t => t.PatientId == item.PatientId && (t.Status == TreatmentPlanStatus.Planned || t.Status == TreatmentPlanStatus.Scheduled))
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .ToListAsync();

        var index = siblings.FindIndex(t => t.Id == id);
        var target = index + Math.Sign(direction);
        if (index >= 0 && target >= 0 && target < siblings.Count)
        {
            (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
            for (var i = 0; i < siblings.Count; i++)
            {
                siblings[i].SortOrder = i;
            }
            await Db.SaveChangesAsync();
        }

        return Back(item.PatientId);
    }

    private IActionResult Back(int patientId) => RedirectToAction("Details", "Patients", new { id = patientId, tab = "plan" });
}
