using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[Authorize]
public class BillingController : Controller
{
    private readonly ApplicationDbContext _db;

    public BillingController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: /Billing
    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments)
            .AsQueryable();

        if (Enum.TryParse<InvoiceStatus>(status, out var parsedStatus))
            query = query.Where(i => i.Status == parsedStatus);

        ViewBag.StatusFilter = status;
        ViewBag.Statuses = Enum.GetNames<InvoiceStatus>();
        return View(await query.OrderByDescending(i => i.InvoiceDate).ToListAsync());
    }

    // GET: /Billing/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Payments.OrderByDescending(p => p.PaymentDate))
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        return View(invoice);
    }

    // GET: /Billing/Create?patientId=5
    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new InvoiceFormViewModel
        {
            PatientId = patientId ?? 0,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    // POST: /Billing/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceFormViewModel vm)
    {
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
        TempData["Success"] = "Invoice created.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // GET: /Billing/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
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
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    // POST: /Billing/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InvoiceFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
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

        await _db.SaveChangesAsync();
        TempData["Success"] = "Invoice updated.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // GET: /Billing/AddPayment/5 (invoiceId)
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
        ViewBag.Invoice = invoice;
        return View(vm);
    }

    // POST: /Billing/AddPayment
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(PaymentFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var inv = await _db.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);
            ViewBag.Invoice = inv;
            return View(vm);
        }

        var invoice = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);

        if (invoice == null) return NotFound();

        var payment = new Payment
        {
            InvoiceId = vm.InvoiceId,
            PaymentDate = vm.PaymentDate,
            Amount = vm.Amount,
            PaymentMethod = vm.PaymentMethod,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);

        // Update invoice status
        var totalPaid = invoice.Payments.Sum(p => p.Amount) + vm.Amount;
        if (totalPaid >= invoice.TotalAmount)
            invoice.Status = InvoiceStatus.Paid;
        else
            invoice.Status = InvoiceStatus.PartiallyPaid;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Payment recorded.";
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
}
