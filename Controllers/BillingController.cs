using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

public class BillingController : ClinicControllerBase
{
    private const int PageSize = 25;
    private readonly ISettingsService _settings;

    public BillingController(ApplicationDbContext db, ISettingsService settings) : base(db)
    {
        _settings = settings;
    }

    public async Task<IActionResult> Index(InvoiceStatus? status, bool overdue = false, string? search = null, int? patientId = null, int pageNumber = 1)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = Db.Invoices.AsNoTracking()
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        if (overdue)
        {
            query = query.Where(i => i.DueDate != null && i.DueDate < today
                                  && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid));
        }

        if (patientId.HasValue)
        {
            query = query.Where(i => i.PatientId == patientId.Value);
        }

        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{SearchNormalizer.Normalize(searchTerm)}%";
            if (int.TryParse(searchTerm.TrimStart('#'), out var number))
            {
                query = query.Where(i => i.Id == number || EF.Functions.Like(i.Patient!.SearchIndex, pattern));
            }
            else
            {
                query = query.Where(i => EF.Functions.Like(i.Patient!.SearchIndex, pattern));
            }
        }

        var vm = new BillingIndexViewModel
        {
            StatusFilter = status,
            OnlyOverdue = overdue,
            Search = searchTerm,
            PatientId = patientId,
            ShowFinancials = HttpContext.CanViewFinancials(),
            Invoices = await PaginatedList<Invoice>.CreateAsync(
                query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id), pageNumber, PageSize)
        };

        if (vm.ShowFinancials)
        {
            var open = await Db.Invoices.AsNoTracking()
                .Include(i => i.Payments)
                .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                .ToListAsync();

            vm.OpenInvoiceCount = open.Count;
            vm.OutstandingTotal = open.Sum(i => i.Balance);
            vm.OverdueTotal = open.Where(i => i.IsOverdue).Sum(i => i.Balance);

            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            vm.CollectedThisMonth = (await Db.Payments
                .Where(p => (!p.IsPlanned || p.IsSettled)
                         && (p.SettledDate ?? p.PaymentDate) >= monthStart
                         && (p.SettledDate ?? p.PaymentDate) < monthEnd
                         && p.Invoice!.Status != InvoiceStatus.Cancelled)
                .Select(p => p.Amount)
                .ToListAsync())
                .Sum();
        }

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await Db.Invoices.AsNoTracking()
            .Include(i => i.Patient)
            .Include(i => i.Items.OrderBy(item => item.SortOrder))
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        return View(invoice);
    }

    [AdminOnly]
    public async Task<IActionResult> Create(int? patientId, string? planItems)
    {
        var vm = new InvoiceFormViewModel
        {
            PatientId = patientId ?? 0,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            FirstPaymentDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1))
        };

        // Pre-fill from completed, unbilled treatment plan items.
        var planIds = (planItems ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();

        if (planIds.Count > 0)
        {
            var items = await Db.TreatmentPlanItems.AsNoTracking()
                .Where(t => planIds.Contains(t.Id) && t.InvoiceId == null)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            if (items.Count > 0)
            {
                vm.PatientId = items[0].PatientId;
                vm.PlanItemIds = string.Join(",", items.Select(i => i.Id));
                vm.Items = items.Select(i => new InvoiceItemInputModel
                {
                    ProcedureId = i.ProcedureId,
                    Description = i.Description,
                    ToothNumbers = i.ToothNumbers,
                    Quantity = 1,
                    UnitPrice = i.EstimatedPrice
                }).ToList();
            }
        }

        if (vm.Items.Count == 0)
        {
            vm.Items.Add(new InvoiceItemInputModel());
        }

        await PopulateAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Create(InvoiceFormViewModel vm)
    {
        var items = ValidateAndCollectItems(vm);
        NormalizePaymentPlanModelState(vm);
        ValidatePaymentPlan(vm);

        if (!ModelState.IsValid)
        {
            await PopulateAsync(vm);
            return View(vm);
        }

        var invoice = new Invoice
        {
            PatientId = vm.PatientId,
            InvoiceDate = vm.InvoiceDate,
            DueDate = vm.DueDate,
            Status = vm.Status,
            Notes = Clean(vm.Notes),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in items)
        {
            invoice.Items.Add(item);
        }

        invoice.TotalAmount = BillingRules.CalculateTotal(invoice.Items);

        if (vm.EnablePaymentPlan && vm.FirstPaymentDate.HasValue)
        {
            foreach (var installment in BillingRules.BuildInstallmentPlan(invoice.TotalAmount, vm.FirstPaymentDate.Value, vm.InstallmentCount, vm.InstallmentIntervalMonths))
            {
                invoice.Payments.Add(installment);
            }
        }

        invoice.Status = BillingRules.DeriveStatus(invoice);

        Db.Invoices.Add(invoice);
        await Db.SaveChangesAsync();
        await LinkPlanItemsAsync(invoice, vm.PlanItemIds);
        Success($"Fatura #{invoice.Id} oluşturuldu.");
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [AdminOnly]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await Db.Invoices.AsNoTracking()
            .Include(i => i.Items.OrderBy(item => item.SortOrder))
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        var planned = invoice.PlannedPayments.ToList();
        var vm = new InvoiceFormViewModel
        {
            Id = invoice.Id,
            PatientId = invoice.PatientId,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status,
            Notes = invoice.Notes,
            Items = invoice.Items.Select(i => new InvoiceItemInputModel
            {
                Id = i.Id,
                ProcedureId = i.ProcedureId,
                Description = i.Description,
                ToothNumbers = i.ToothNumbers,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            EnablePaymentPlan = planned.Count > 0,
            FirstPaymentDate = planned.Select(p => (DateOnly?)p.PaymentDate).FirstOrDefault() ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(1)),
            InstallmentCount = planned.Count == 0 ? 1 : planned.Count,
            InstallmentIntervalMonths = InferInterval(planned),
            PlanLocked = planned.Any(p => p.IsSettled),
            ExistingPlannedPayments = planned
        };

        if (vm.Items.Count == 0)
        {
            vm.Items.Add(new InvoiceItemInputModel());
        }

        await PopulateAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Edit(int id, InvoiceFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        var invoice = await Db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        var planLocked = invoice.PlannedPayments.Any(p => p.IsSettled);
        var items = ValidateAndCollectItems(vm);
        NormalizePaymentPlanModelState(vm);
        if (!planLocked)
        {
            ValidatePaymentPlan(vm);
        }

        if (!ModelState.IsValid)
        {
            vm.PlanLocked = planLocked;
            vm.ExistingPlannedPayments = invoice.PlannedPayments.ToList();
            await PopulateAsync(vm);
            return View(vm);
        }

        invoice.PatientId = vm.PatientId;
        invoice.InvoiceDate = vm.InvoiceDate;
        invoice.DueDate = vm.DueDate;
        invoice.Status = vm.Status;
        invoice.Notes = Clean(vm.Notes);
        invoice.UpdatedAt = DateTime.UtcNow;

        // Replace line items: update matching ids, remove missing, add new.
        var incomingIds = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        foreach (var existing in invoice.Items.Where(i => !incomingIds.Contains(i.Id)).ToList())
        {
            Db.InvoiceItems.Remove(existing);
            invoice.Items.Remove(existing);
        }

        foreach (var item in items)
        {
            var target = item.Id > 0 ? invoice.Items.FirstOrDefault(i => i.Id == item.Id) : null;
            if (target == null)
            {
                item.Id = 0;
                invoice.Items.Add(item);
            }
            else
            {
                target.ProcedureId = item.ProcedureId;
                target.Description = item.Description;
                target.ToothNumbers = item.ToothNumbers;
                target.Quantity = item.Quantity;
                target.UnitPrice = item.UnitPrice;
                target.SortOrder = item.SortOrder;
            }
        }

        invoice.TotalAmount = BillingRules.CalculateTotal(invoice.Items);

        if (!planLocked)
        {
            foreach (var oldInstallment in invoice.Payments.Where(p => p.IsPlanned).ToList())
            {
                Db.Payments.Remove(oldInstallment);
                invoice.Payments.Remove(oldInstallment);
            }

            if (vm.EnablePaymentPlan && vm.FirstPaymentDate.HasValue)
            {
                foreach (var installment in BillingRules.BuildInstallmentPlan(invoice.TotalAmount, vm.FirstPaymentDate.Value, vm.InstallmentCount, vm.InstallmentIntervalMonths))
                {
                    invoice.Payments.Add(installment);
                }
            }
        }

        invoice.Status = BillingRules.DeriveStatus(invoice);

        await Db.SaveChangesAsync();
        Success("Fatura güncellendi.");
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [AdminOnly]
    public async Task<IActionResult> AddPayment(int invoiceId, int? plannedPaymentId)
    {
        var invoice = await Db.Invoices.AsNoTracking()
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null) return NotFound();

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            Error("İptal edilmiş faturaya ödeme eklenemez.");
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        var vm = new PaymentFormViewModel
        {
            InvoiceId = invoiceId,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            Amount = Math.Max(invoice.Balance, 0),
            PlannedPaymentId = plannedPaymentId,
            Invoice = invoice
        };

        var selectedInstallment = plannedPaymentId.HasValue
            ? invoice.Payments.FirstOrDefault(p => p.Id == plannedPaymentId.Value && p.IsPlanned && !p.IsSettled)
            : invoice.PlannedPayments.FirstOrDefault(p => !p.IsSettled);

        if (selectedInstallment != null)
        {
            vm.PlannedPaymentId = selectedInstallment.Id;
            vm.Amount = selectedInstallment.Amount;
        }

        PopulatePlannedPayments(vm, invoice);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> AddPayment(PaymentFormViewModel vm)
    {
        var invoice = await Db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);

        if (invoice == null) return NotFound();

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            ModelState.AddModelError(string.Empty, "İptal edilmiş faturaya ödeme eklenemez.");
        }

        Payment? plannedPayment = null;
        if (vm.PlannedPaymentId.HasValue)
        {
            plannedPayment = invoice.Payments.FirstOrDefault(p => p.Id == vm.PlannedPaymentId.Value && p.IsPlanned && !p.IsSettled);
            if (plannedPayment == null)
            {
                ModelState.AddModelError(nameof(vm.PlannedPaymentId), "Seçilen taksit bulunamadı veya zaten tahsil edilmiş.");
            }
        }

        if (!ModelState.IsValid)
        {
            vm.Invoice = invoice;
            PopulatePlannedPayments(vm, invoice);
            return View(vm);
        }

        if (plannedPayment != null)
        {
            plannedPayment.Amount = vm.Amount;
            plannedPayment.PaymentMethod = vm.PaymentMethod;
            plannedPayment.Notes = Clean(vm.Notes);
            plannedPayment.IsSettled = true;
            plannedPayment.SettledDate = vm.PaymentDate;
        }
        else
        {
            invoice.Payments.Add(new Payment
            {
                PaymentDate = vm.PaymentDate,
                SettledDate = vm.PaymentDate,
                Amount = vm.Amount,
                PaymentMethod = vm.PaymentMethod,
                Notes = Clean(vm.Notes),
                IsPlanned = false,
                IsSettled = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        invoice.Status = BillingRules.DeriveStatus(invoice);
        invoice.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        Success($"{vm.Amount.ToMoney()} tahsilat kaydedildi.");
        return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var payment = await Db.Payments.Include(p => p.Invoice!).ThenInclude(i => i.Payments).FirstOrDefaultAsync(p => p.Id == id);
        if (payment == null || payment.Invoice == null) return NotFound();

        var invoice = payment.Invoice;

        if (payment.IsPlanned)
        {
            // Un-settle an installment instead of deleting it so the plan stays intact.
            payment.IsSettled = false;
            payment.SettledDate = null;
            payment.Notes = null;
            Success("Taksit tahsilatı geri alındı.");
        }
        else
        {
            invoice.Payments.Remove(payment);
            Db.Payments.Remove(payment);
            Success("Ödeme kaydı silindi.");
        }

        invoice.Status = BillingRules.DeriveStatus(invoice);
        invoice.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Cancel(int id)
    {
        var invoice = await Db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        if (invoice.TotalPaid > 0)
        {
            Error("Tahsilatı olan bir fatura iptal edilemez. Önce ödemeleri silin.");
            return RedirectToAction(nameof(Details), new { id });
        }

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        Success("Fatura iptal edildi.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Reopen(int id)
    {
        var invoice = await Db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        invoice.Status = InvoiceStatus.Issued;
        invoice.Status = BillingRules.DeriveStatus(invoice);
        invoice.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        Success("Fatura yeniden açıldı.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await Db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        if (invoice.TotalPaid > 0)
        {
            Error("Tahsilatı olan bir fatura silinemez. Önce ödemeleri silin veya faturayı iptal edin.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var patientId = invoice.PatientId;
        Db.Invoices.Remove(invoice);
        await Db.SaveChangesAsync();
        Success($"Fatura #{id} silindi.");
        return RedirectToAction("Details", "Patients", new { id = patientId, tab = "invoices" });
    }

    private List<InvoiceItem> ValidateAndCollectItems(InvoiceFormViewModel vm)
    {
        var result = new List<InvoiceItem>();
        var order = 0;

        for (var index = 0; index < vm.Items.Count; index++)
        {
            var input = vm.Items[index];
            if (input.IsEmpty)
            {
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith($"Items[{index}].", StringComparison.Ordinal)).ToList())
                {
                    ModelState.Remove(key);
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(input.Description))
            {
                ModelState.AddModelError($"Items[{index}].Description", "İşlem adı zorunludur.");
            }

            result.Add(new InvoiceItem
            {
                Id = input.Id,
                ProcedureId = input.ProcedureId,
                Description = input.Description?.Trim() ?? string.Empty,
                ToothNumbers = Clean(input.ToothNumbers),
                Quantity = input.Quantity,
                UnitPrice = Math.Round(input.UnitPrice, 2, MidpointRounding.AwayFromZero),
                SortOrder = order++
            });
        }

        if (result.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Faturaya en az bir işlem satırı ekleyin.");
        }

        return result;
    }

    private void ValidatePaymentPlan(InvoiceFormViewModel vm)
    {
        if (!vm.EnablePaymentPlan)
        {
            return;
        }

        if (!vm.FirstPaymentDate.HasValue)
        {
            ModelState.AddModelError(nameof(InvoiceFormViewModel.FirstPaymentDate), "İlk taksit tarihi zorunludur.");
        }

        if (vm.InstallmentCount < 2)
        {
            ModelState.AddModelError(nameof(InvoiceFormViewModel.InstallmentCount), "Taksitli plan için en az 2 taksit girin.");
        }
    }

    private void NormalizePaymentPlanModelState(InvoiceFormViewModel vm)
    {
        if (vm.EnablePaymentPlan)
        {
            return;
        }

        ModelState.Remove(nameof(InvoiceFormViewModel.FirstPaymentDate));
        ModelState.Remove(nameof(InvoiceFormViewModel.InstallmentCount));
        ModelState.Remove(nameof(InvoiceFormViewModel.InstallmentIntervalMonths));
        vm.InstallmentCount = Math.Max(1, vm.InstallmentCount);
        vm.InstallmentIntervalMonths = Math.Max(1, vm.InstallmentIntervalMonths);
    }

    private async Task PopulateAsync(InvoiceFormViewModel vm)
    {
        vm.Patients = await GetPatientSelectListAsync(vm.PatientId == 0 ? null : vm.PatientId);
        vm.Procedures = await Db.Procedures.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync();

        if (vm.PatientId > 0)
        {
            vm.PatientName = await Db.Patients.Where(p => p.Id == vm.PatientId).Select(p => p.FullName).FirstOrDefaultAsync();
        }
    }

    private static void PopulatePlannedPayments(PaymentFormViewModel vm, Invoice invoice)
    {
        vm.PlannedPayments = invoice.PlannedPayments
            .Where(p => !p.IsSettled)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.InstallmentNumber}. taksit · {p.PaymentDate.ToShortDate()} · {p.Amount.ToMoney()}",
                Selected = vm.PlannedPaymentId == p.Id
            })
            .ToList();
    }

    private static int InferInterval(IReadOnlyList<Payment> planned)
    {
        if (planned.Count < 2) return 1;
        var first = planned[0].PaymentDate;
        var second = planned[1].PaymentDate;
        var months = (second.Year - first.Year) * 12 + second.Month - first.Month;
        return Math.Clamp(months, 1, 12);
    }

    private async Task LinkPlanItemsAsync(Invoice invoice, string? planItemIds)
    {
        var ids = (planItemIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();

        if (ids.Count == 0) return;

        var items = await Db.TreatmentPlanItems.Where(t => ids.Contains(t.Id) && t.PatientId == invoice.PatientId && t.InvoiceId == null).ToListAsync();
        foreach (var item in items)
        {
            item.InvoiceId = invoice.Id;
        }

        if (items.Count > 0)
        {
            await Db.SaveChangesAsync();
        }
    }

    /// <summary>End-of-day cash report: what was collected, by which method, and what was invoiced.</summary>
    [AdminOnly]
    public async Task<IActionResult> DailyReport(DateOnly? date)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.Today);
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var clinic = await _settings.GetClinicSettingsAsync();

        var payments = await Db.Payments.AsNoTracking()
            .Include(p => p.Invoice!).ThenInclude(i => i.Patient)
            .Where(p => (!p.IsPlanned || p.IsSettled) && (p.SettledDate ?? p.PaymentDate) == day && p.Invoice!.Status != InvoiceStatus.Cancelled)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var vm = new DailyReportViewModel
        {
            Date = day,
            ClinicName = clinic.ClinicName,
            Payments = payments,
            InvoicesIssued = await Db.Invoices.AsNoTracking().Include(i => i.Patient).Include(i => i.Payments)
                .Where(i => i.InvoiceDate == day && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
                .OrderBy(i => i.Id).ToListAsync(),
            DueInstallments = await Db.Payments.AsNoTracking().Include(p => p.Invoice!).ThenInclude(i => i.Patient)
                .Where(p => p.IsPlanned && !p.IsSettled && p.PaymentDate <= day && p.Invoice!.Status != InvoiceStatus.Cancelled)
                .OrderBy(p => p.PaymentDate).ToListAsync(),
            AppointmentsTotal = await Db.Appointments.CountAsync(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd && a.Status != AppointmentStatus.Cancelled),
            AppointmentsCompleted = await Db.Appointments.CountAsync(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd && a.Status == AppointmentStatus.Completed),
            AppointmentsNoShow = await Db.Appointments.CountAsync(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd && a.Status == AppointmentStatus.NoShow)
        };

        return View(vm);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
