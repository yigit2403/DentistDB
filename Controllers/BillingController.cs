using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class BillingController : Controller
{
    private readonly ApplicationDbContext _db;

    public BillingController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .AsQueryable();

        if (Enum.TryParse<InvoiceStatus>(status, out var parsedStatus))
            query = query.Where(i => i.Status == parsedStatus);

        ViewBag.StatusFilter = status;
        ViewBag.Statuses = Enum.GetValues<InvoiceStatus>();
        return View(await query.OrderByDescending(i => i.InvoiceDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments.OrderByDescending(p => p.PaymentDate))
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        return View(invoice);
    }

    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new InvoiceFormViewModel
        {
            PatientId = patientId ?? 0,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            FirstInstallmentDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceFormViewModel vm)
    {
        ValidateInstallments(vm);
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var invoice = new Invoice
        {
            PatientId = vm.PatientId,
            InvoiceDate = vm.InvoiceDate,
            DueDate = vm.DueDate,
            TotalAmount = vm.TotalAmount,
            Status = vm.Status,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        await ReplaceInstallmentsAsync(invoice, vm);
        TempData["Success"] = "Fatura oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [AdminOnly]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        var vm = new InvoiceFormViewModel
        {
            Id = invoice.Id,
            PatientId = invoice.PatientId,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status,
            Notes = invoice.Notes,
            EnableInstallments = invoice.Payments.Any(p => p.IsPlanned),
            FirstInstallmentDate = invoice.Payments.Where(p => p.IsPlanned).OrderBy(p => p.PaymentDate).Select(p => (DateOnly?)p.PaymentDate).FirstOrDefault(),
            InstallmentCount = invoice.Payments.Count(p => p.IsPlanned),
            ExistingInstallments = invoice.Payments
                .Where(p => p.IsPlanned)
                .OrderBy(p => p.PaymentDate)
                .Select(p => new InstallmentViewModel
                {
                    Id = p.Id,
                    PaymentDate = p.PaymentDate,
                    Amount = p.Amount,
                    IsSettled = p.IsSettled,
                    InstallmentNumber = p.InstallmentNumber
                })
                .ToList(),
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Edit(int id, InvoiceFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        ValidateInstallments(vm);
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();

        invoice.PatientId = vm.PatientId;
        invoice.InvoiceDate = vm.InvoiceDate;
        invoice.DueDate = vm.DueDate;
        invoice.TotalAmount = vm.TotalAmount;
        invoice.Status = vm.Status;
        invoice.Notes = vm.Notes;
        invoice.UpdatedAt = DateTime.UtcNow;

        await ReplaceInstallmentsAsync(invoice, vm);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Fatura güncellendi.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [AdminOnly]
    public async Task<IActionResult> AddPayment(int invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null) return NotFound();

        var vm = new PaymentFormViewModel
        {
            InvoiceId = invoiceId,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            Amount = invoice.Balance
        };
        await PopulateInstallmentsAsync(vm);
        ViewBag.Invoice = invoice;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> AddPayment(PaymentFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var inv = await _db.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);
            await PopulateInstallmentsAsync(vm);
            ViewBag.Invoice = inv;
            return View(vm);
        }

        var invoice = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);

        if (invoice == null) return NotFound();

        if (vm.InstallmentId.HasValue)
        {
            var installment = invoice.Payments.FirstOrDefault(p => p.Id == vm.InstallmentId.Value && p.IsPlanned);
            if (installment == null)
            {
                ModelState.AddModelError(nameof(PaymentFormViewModel.InstallmentId), "Seçilen taksit kaydı bulunamadı.");
                await PopulateInstallmentsAsync(vm);
                ViewBag.Invoice = invoice;
                return View(vm);
            }

            installment.Amount = vm.Amount;
            installment.PaymentMethod = vm.PaymentMethod;
            installment.Notes = vm.Notes;
            installment.IsSettled = true;
            installment.SettledDate = vm.PaymentDate;
        }
        else
        {
            var payment = new Payment
            {
                InvoiceId = vm.InvoiceId,
                PaymentDate = vm.PaymentDate,
                Amount = vm.Amount,
                PaymentMethod = vm.PaymentMethod,
                Notes = vm.Notes,
                IsPlanned = false,
                IsSettled = true,
                SettledDate = vm.PaymentDate,
                CreatedAt = DateTime.UtcNow
            };
            _db.Payments.Add(payment);
            invoice.Payments.Add(payment);
        }

        SyncInvoiceStatus(invoice);
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Ödeme kaydı oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
    }

    private async Task<IEnumerable<SelectListItem>> GetPatientSelectList()
    {
        return await _db.Patients
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.FullName)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName })
            .ToListAsync();
    }

    private void ValidateInstallments(InvoiceFormViewModel vm)
    {
        if (!vm.EnableInstallments)
        {
            return;
        }

        if (!vm.FirstInstallmentDate.HasValue)
        {
            ModelState.AddModelError(nameof(InvoiceFormViewModel.FirstInstallmentDate), "İlk taksit tarihi zorunludur.");
        }
    }

    private async Task ReplaceInstallmentsAsync(Invoice invoice, InvoiceFormViewModel vm)
    {
        var existingInstallments = await _db.Payments.Where(p => p.InvoiceId == invoice.Id && p.IsPlanned).ToListAsync();
        var hasSettledPayments = await _db.Payments.AnyAsync(p => p.InvoiceId == invoice.Id && ((!p.IsPlanned && p.IsSettled) || (p.IsPlanned && p.IsSettled)));

        if (hasSettledPayments && existingInstallments.Any())
        {
            return;
        }

        if (existingInstallments.Any())
        {
            _db.Payments.RemoveRange(existingInstallments);
        }

        if (!vm.EnableInstallments || !vm.FirstInstallmentDate.HasValue)
        {
            return;
        }

        var installmentAmount = Math.Round(invoice.TotalAmount / vm.InstallmentCount, 2, MidpointRounding.AwayFromZero);
        var runningTotal = 0m;

        for (var i = 1; i <= vm.InstallmentCount; i++)
        {
            var amount = i == vm.InstallmentCount
                ? invoice.TotalAmount - runningTotal
                : installmentAmount;

            runningTotal += amount;

            _db.Payments.Add(new Payment
            {
                InvoiceId = invoice.Id,
                PaymentDate = vm.FirstInstallmentDate.Value.AddMonths((i - 1) * vm.InstallmentIntervalMonths),
                Amount = amount,
                PaymentMethod = PaymentMethod.Other,
                Notes = "Otomatik oluşturulan taksit kaydı",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = i,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task PopulateInstallmentsAsync(PaymentFormViewModel vm)
    {
        vm.Installments = await _db.Payments
            .Where(p => p.InvoiceId == vm.InvoiceId && p.IsPlanned && !p.IsSettled)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"Taksit {(p.InstallmentNumber ?? 0)} - {p.PaymentDate:dd.MM.yyyy} - {p.Amount:C}"
            })
            .ToListAsync();
    }

    private static void SyncInvoiceStatus(Invoice invoice)
    {
        if (invoice.TotalPaid >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (invoice.TotalPaid > 0)
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }
        else if (invoice.Status != InvoiceStatus.Cancelled)
        {
            invoice.Status = InvoiceStatus.Issued;
        }
    }
}
