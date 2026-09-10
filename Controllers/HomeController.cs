using System.Diagnostics;
using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

public class HomeController : ClinicControllerBase
{
    public HomeController(ApplicationDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(7);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var todayDate = DateOnly.FromDateTime(today);
        var monthStartDate = DateOnly.FromDateTime(monthStart);
        var monthEndDate = DateOnly.FromDateTime(monthEnd);
        var showFinancials = HttpContext.CanViewFinancials();

        var vm = new DashboardViewModel
        {
            Today = today,
            ShowFinancials = showFinancials,

            TodaysAppointments = await Db.Appointments
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate >= today && a.AppointmentDate < tomorrow)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(),

            UpcomingAppointments = await Db.Appointments
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate >= tomorrow && a.AppointmentDate < today.AddDays(8)
                         && a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.AppointmentDate)
                .Take(10)
                .ToListAsync(),

            RecentOperations = await Db.PreviousOperations
                .Include(o => o.Patient)
                .OrderByDescending(o => o.Date)
                .ThenByDescending(o => o.UpdatedAt)
                .Take(6)
                .ToListAsync(),

            RecentPatients = (await Db.Patients
                .Where(p => !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .Select(p => new
                {
                    p.Id,
                    p.FullName,
                    p.Phone,
                    p.MedicalAlerts,
                    p.ArrivalDate,
                    p.CreatedAt,
                    p.PhotoContentType
                })
                .ToListAsync())
                .Select(p => new Patient
                {
                    Id = p.Id,
                    FullName = p.FullName,
                    Phone = p.Phone,
                    MedicalAlerts = p.MedicalAlerts,
                    ArrivalDate = p.ArrivalDate,
                    CreatedAt = p.CreatedAt,
                    PhotoContentType = p.PhotoContentType
                })
                .ToList(),

            TotalPatients = await Db.Patients.CountAsync(p => !p.IsArchived),
            NewPatientsThisMonth = await Db.Patients.CountAsync(p => p.CreatedAt >= monthStart.ToUniversalTime()),
            AppointmentsThisWeek = await Db.Appointments.CountAsync(a => a.AppointmentDate >= weekStart && a.AppointmentDate < weekEnd && a.Status != AppointmentStatus.Cancelled),
            AppointmentsThisMonth = await Db.Appointments.CountAsync(a => a.AppointmentDate >= monthStart && a.AppointmentDate < monthEnd && a.Status != AppointmentStatus.Cancelled)
        };

        if (showFinancials)
        {
            vm.DuePaymentPlanItems = await Db.Payments
                .Include(p => p.Invoice!).ThenInclude(i => i.Patient)
                .Where(p => p.IsPlanned && !p.IsSettled && p.PaymentDate <= todayDate.AddDays(14)
                         && p.Invoice!.Status != InvoiceStatus.Cancelled)
                .OrderBy(p => p.PaymentDate)
                .Take(8)
                .ToListAsync();

            var openInvoices = await Db.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Payments)
                .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                .ToListAsync();

            vm.OutstandingBalance = openInvoices.Sum(i => i.Balance);
            vm.OverdueInvoices = openInvoices
                .Where(i => i.IsOverdue && i.Balance > 0)
                .OrderBy(i => i.DueDate)
                .Take(6)
                .ToList();

            vm.CollectedThisMonth = (await Db.Payments
                .Where(p => (!p.IsPlanned || p.IsSettled)
                         && (p.SettledDate ?? p.PaymentDate) >= monthStartDate
                         && (p.SettledDate ?? p.PaymentDate) < monthEndDate
                         && p.Invoice!.Status != InvoiceStatus.Cancelled)
                .Select(p => p.Amount)
                .ToListAsync())
                .Sum();
        }

        return View(vm);
    }

    [AllowAnonymousAccount]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        Response.StatusCode = code is >= 400 and < 600 ? code.Value : 500;
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = Response.StatusCode
        });
    }
}
