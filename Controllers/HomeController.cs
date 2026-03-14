using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[Authorize]
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
        var tomorrow = today.AddDays(1);
        var nextWeek = today.AddDays(7);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1);

        var vm = new DashboardViewModel
        {
            TodaysAppointments = await _db.Appointments
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate >= today && a.AppointmentDate < tomorrow
                         && a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(),

            UpcomingAppointments = await _db.Appointments
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate >= tomorrow && a.AppointmentDate < nextWeek
                         && a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.AppointmentDate)
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
                .OrderBy(i => i.DueDate)
                .ToListAsync(),

            TotalPatients = await _db.Patients.CountAsync(p => !p.IsArchived),

            AppointmentsThisMonth = await _db.Appointments
                .CountAsync(a => a.AppointmentDate >= startOfMonth && a.AppointmentDate < endOfMonth)
        };

        return View(vm);
    }
}
