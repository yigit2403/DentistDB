using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var todayDateOnly = DateOnly.FromDateTime(today);

        var vm = new DashboardViewModel
        {
            UpcomingAppointments = await _db.Appointments
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate >= today && a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.AppointmentDate)
                .Take(10)
                .ToListAsync(),

            UpcomingInstallments = await _db.Payments
                .Include(p => p.Invoice!)
                .ThenInclude(i => i.Patient)
                .Where(p => p.IsPlanned && !p.IsSettled && p.PaymentDate >= todayDateOnly)
                .OrderBy(p => p.PaymentDate)
                .Take(8)
                .ToListAsync(),

            RecentOperations = await _db.PreviousOperations
                .Include(o => o.Patient)
                .OrderByDescending(o => o.Date)
                .ThenByDescending(o => o.UpdatedAt)
                .Take(8)
                .ToListAsync(),

            RecentPatients = await _db.Patients
                .Where(p => !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync(),

            UnpaidInvoices = await _db.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Payments)
                .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                .OrderBy(i => i.Patient!.FullName)
                .ToListAsync()
        };

        return View(vm);
    }
}
