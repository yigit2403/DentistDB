using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class BillingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PatientFinanceService _financeService;

    public BillingController(ApplicationDbContext db, PatientFinanceService financeService)
    {
        _db = db;
        _financeService = financeService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .AsQueryable();

        if (Enum.TryParse<InvoiceStatus>(status, out var parsedStatus))
        {
            query = query.Where(i => i.Status == parsedStatus);
        }

        ViewBag.StatusFilter = status;
        ViewBag.Statuses = Enum.GetValues<InvoiceStatus>();
        return View(await query.OrderBy(i => i.Patient!.FullName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments.OrderByDescending(p => p.PaymentDate))
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    public async Task<IActionResult> Create(int? patientId)
    {
        InvoiceFormViewModel vm;
        var existingAccount = patientId.HasValue
            ? await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.PatientId == patientId.Value)
            : null;

        if (existingAccount != null)
        {
            vm = MapInvoiceForm(existingAccount);
            vm.IsExistingAccount = true;
        }
        else
        {
            vm = new InvoiceFormViewModel
            {
                PatientId = patientId ?? 0,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                FirstInstallmentDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
            };
        }

        vm.Patients = await GetPatientSelectList();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceFormViewModel vm)
    {
        var account = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.PatientId == vm.PatientId);

        await ValidateInvoiceFormAsync(vm, account);
        if (!ModelState.IsValid)
        {
            vm.IsExistingAccount = account != null;
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var isNewAccount = account == null;
        account ??= new Invoice
        {
            PatientId = vm.PatientId,
            CreatedAt = DateTime.UtcNow
        };

        account.InvoiceDate = vm.InvoiceDate;
        account.DueDate = vm.DueDate;
        account.TotalAmount = vm.TotalAmount;
        account.Status = vm.Status;
        account.Notes = vm.Notes;
        account.UpdatedAt = DateTime.UtcNow;

        if (isNewAccount)
        {
            _db.Invoices.Add(account);
            await _db.SaveChangesAsync();
        }

        await SyncManualInstallmentsAsync(account, vm);
        PatientFinanceService.SyncInvoiceStatus(account);
        await _db.SaveChangesAsync();

        TempData["Success"] = isNewAccount ? "Finans hesabı oluşturuldu." : "Finans hesabı güncellendi.";
        return RedirectToAction(nameof(Details), new { id = account.Id });
    }

    [AdminOnly]
    public async Task<IActionResult> Edit(int id)
    {
        var account = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (account == null)
        {
            return NotFound();
        }

        var vm = MapInvoiceForm(account);
        vm.IsExistingAccount = true;
        vm.Patients = await GetPatientSelectList();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Edit(int id, InvoiceFormViewModel vm)
    {
        if (id != vm.Id)
        {
            return BadRequest();
        }

        var account = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (account == null)
        {
            return NotFound();
        }

        await ValidateInvoiceFormAsync(vm, account);
        if (!ModelState.IsValid)
        {
            vm.IsExistingAccount = true;
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        account.PatientId = vm.PatientId;
        account.InvoiceDate = vm.InvoiceDate;
        account.DueDate = vm.DueDate;
        account.TotalAmount = vm.TotalAmount;
        account.Status = vm.Status;
        account.Notes = vm.Notes;
        account.UpdatedAt = DateTime.UtcNow;

        await SyncManualInstallmentsAsync(account, vm);
        PatientFinanceService.SyncInvoiceStatus(account);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Finans hesabı güncellendi.";
        return RedirectToAction(nameof(Details), new { id = account.Id });
    }

    [AdminOnly]
    public async Task<IActionResult> AddPayment(int invoiceId, int? installmentId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
        {
            return NotFound();
        }

        var vm = new PaymentFormViewModel
        {
            InvoiceId = invoiceId,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            Amount = invoice.Balance
        };

        if (installmentId.HasValue)
        {
            var installment = invoice.Payments.FirstOrDefault(p => p.Id == installmentId.Value && p.IsPlanned && !p.IsSettled);
            if (installment == null)
            {
                return NotFound();
            }

            vm.InstallmentId = installment.Id;
            vm.Amount = installment.Amount;
        }

        await PopulateInstallmentsAsync(vm);
        ViewBag.Invoice = invoice;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> AddPayment(PaymentFormViewModel vm)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);

        if (invoice == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateInstallmentsAsync(vm);
            ViewBag.Invoice = invoice;
            return View(vm);
        }

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

        PatientFinanceService.SyncInvoiceStatus(invoice);
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

    private static InvoiceFormViewModel MapInvoiceForm(Invoice account)
    {
        var plannedInstallments = account.Payments
            .Where(p => p.IsPlanned)
            .OrderBy(p => p.PaymentDate)
            .ToList();

        return new InvoiceFormViewModel
        {
            Id = account.Id,
            PatientId = account.PatientId,
            InvoiceDate = account.InvoiceDate,
            DueDate = account.DueDate,
            TotalAmount = account.TotalAmount,
            Status = account.Status,
            Notes = account.Notes,
            EnableInstallments = plannedInstallments.Count > 0,
            FirstInstallmentDate = plannedInstallments.Select(p => (DateOnly?)p.PaymentDate).FirstOrDefault(),
            InstallmentCount = plannedInstallments.Count > 0 ? plannedInstallments.Count : 1,
            ExistingInstallments = plannedInstallments
                .Select(p => new InstallmentViewModel
                {
                    Id = p.Id,
                    PaymentDate = p.PaymentDate,
                    Amount = p.Amount,
                    IsSettled = p.IsSettled,
                    InstallmentNumber = p.InstallmentNumber
                })
                .ToList()
        };
    }

    private async Task ValidateInvoiceFormAsync(InvoiceFormViewModel vm, Invoice? currentAccount)
    {
        if (vm.EnableInstallments && !vm.FirstInstallmentDate.HasValue)
        {
            ModelState.AddModelError(nameof(InvoiceFormViewModel.FirstInstallmentDate), "İlk taksit tarihi zorunludur.");
        }

        if (currentAccount != null && currentAccount.TotalPaid > vm.TotalAmount)
        {
            ModelState.AddModelError(nameof(InvoiceFormViewModel.TotalAmount), "Toplam tutar mevcut ödenen tutardan küçük olamaz.");
        }

        var duplicateAccount = await _db.Invoices
            .AnyAsync(i => i.PatientId == vm.PatientId && (currentAccount == null || i.Id != currentAccount.Id));

        if (duplicateAccount)
        {
            ModelState.AddModelError(nameof(InvoiceFormViewModel.PatientId), "Seçilen hasta için zaten bir finans hesabı bulunuyor.");
        }
    }

    private async Task SyncManualInstallmentsAsync(Invoice account, InvoiceFormViewModel vm)
    {
        var plannedInstallments = account.Payments
            .Where(p => p.IsPlanned)
            .ToList();

        var hasSettledPayments = account.Payments.Any(p => (!p.IsPlanned && p.IsSettled) || (p.IsPlanned && p.IsSettled));

        if (!vm.EnableInstallments)
        {
            if (!hasSettledPayments && plannedInstallments.Count > 0)
            {
                _db.Payments.RemoveRange(plannedInstallments);
                foreach (var installment in plannedInstallments)
                {
                    account.Payments.Remove(installment);
                }
            }

            return;
        }

        await _financeService.ReplaceInstallmentsAsync(
            account,
            vm.FirstInstallmentDate!.Value,
            vm.InstallmentCount,
            vm.InstallmentIntervalMonths,
            "Otomatik oluşturulan taksit kaydı");
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
}
