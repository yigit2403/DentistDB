using DentistDB.Data;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Services;

public class PatientFinanceService
{
    private readonly ApplicationDbContext _db;

    public PatientFinanceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<int?> GetAccountIdAsync(int patientId)
    {
        return _db.Invoices
            .Where(i => i.PatientId == patientId)
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync();
    }

    public Task<Invoice?> GetAccountAsync(int patientId, bool includePayments = false)
    {
        IQueryable<Invoice> query = _db.Invoices;
        if (includePayments)
        {
            query = query.Include(i => i.Payments);
        }

        return query.FirstOrDefaultAsync(i => i.PatientId == patientId);
    }

    public async Task<Invoice> GetOrCreateAccountAsync(int patientId, DateOnly invoiceDate, DateOnly? dueDate, string? notes, bool includePayments = false)
    {
        var account = await GetAccountAsync(patientId, includePayments);
        if (account != null)
        {
            if (dueDate.HasValue)
            {
                account.DueDate = dueDate;
            }

            if (!string.IsNullOrWhiteSpace(notes) && string.IsNullOrWhiteSpace(account.Notes))
            {
                account.Notes = notes.Trim();
            }

            account.UpdatedAt = DateTime.UtcNow;
            return account;
        }

        account = new Invoice
        {
            PatientId = patientId,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            TotalAmount = 0m,
            Status = InvoiceStatus.Issued,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Invoices.Add(account);
        await _db.SaveChangesAsync();

        if (includePayments)
        {
            await EnsurePaymentsLoadedAsync(account);
        }

        return account;
    }

    public void AddCharge(Invoice account, decimal amount, DateOnly? dueDate, string? notes)
    {
        account.TotalAmount += amount;
        if (dueDate.HasValue)
        {
            account.DueDate = dueDate;
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            account.Notes = AppendNote(account.Notes, notes.Trim());
        }

        account.UpdatedAt = DateTime.UtcNow;
        SyncInvoiceStatus(account);
    }

    public async Task AddImmediatePaymentAsync(Invoice account, decimal amount, PaymentMethod paymentMethod, string? paymentNotes, DateOnly paymentDate)
    {
        await EnsurePaymentsLoadedAsync(account);

        var payment = new Payment
        {
            InvoiceId = account.Id,
            PaymentDate = paymentDate,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Notes = paymentNotes,
            IsPlanned = false,
            IsSettled = true,
            SettledDate = paymentDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        account.Payments.Add(payment);
        account.UpdatedAt = DateTime.UtcNow;
        SyncInvoiceStatus(account);
    }

    public async Task ApplyInstallmentChargeAsync(
        Invoice account,
        decimal amount,
        DateOnly firstInstallmentDate,
        int installmentCount,
        int intervalMonths,
        InstallmentMergeMode? mergeMode,
        string note)
    {
        await EnsurePaymentsLoadedAsync(account);

        var unpaidInstallments = account.Payments
            .Where(p => p.IsPlanned && !p.IsSettled)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.InstallmentNumber)
            .ToList();

        if (unpaidInstallments.Count == 0)
        {
            CreateInstallments(account, amount, firstInstallmentDate, installmentCount, intervalMonths, GetNextInstallmentNumber(account), note);
        }
        else if (mergeMode == InstallmentMergeMode.SpreadOverCurrentDates)
        {
            RedistributeInstallments(unpaidInstallments, unpaidInstallments.Sum(p => p.Amount) + amount);
        }
        else
        {
            var nextInstallmentDate = unpaidInstallments.Last().PaymentDate.AddMonths(intervalMonths);
            CreateInstallments(account, amount, nextInstallmentDate, installmentCount, intervalMonths, GetNextInstallmentNumber(account), note);
        }

        account.UpdatedAt = DateTime.UtcNow;
        SyncInvoiceStatus(account);
    }

    public async Task ReplaceInstallmentsAsync(Invoice account, DateOnly firstInstallmentDate, int installmentCount, int intervalMonths, string note)
    {
        await EnsurePaymentsLoadedAsync(account);

        var existingInstallments = account.Payments
            .Where(p => p.IsPlanned)
            .ToList();

        var hasSettledPayments = account.Payments.Any(p => (!p.IsPlanned && p.IsSettled) || (p.IsPlanned && p.IsSettled));
        if (hasSettledPayments && existingInstallments.Count > 0)
        {
            return;
        }

        if (existingInstallments.Count > 0)
        {
            _db.Payments.RemoveRange(existingInstallments);
            foreach (var installment in existingInstallments)
            {
                account.Payments.Remove(installment);
            }
        }

        if (account.Balance <= 0)
        {
            account.UpdatedAt = DateTime.UtcNow;
            SyncInvoiceStatus(account);
            return;
        }

        CreateInstallments(account, account.Balance, firstInstallmentDate, installmentCount, intervalMonths, 1, note);
        account.UpdatedAt = DateTime.UtcNow;
        SyncInvoiceStatus(account);
    }

    public static void SyncInvoiceStatus(Invoice account)
    {
        if (account.TotalPaid >= account.TotalAmount)
        {
            account.Status = InvoiceStatus.Paid;
        }
        else if (account.TotalPaid > 0)
        {
            account.Status = InvoiceStatus.PartiallyPaid;
        }
        else if (account.Status != InvoiceStatus.Cancelled)
        {
            account.Status = InvoiceStatus.Issued;
        }
    }

    private async Task EnsurePaymentsLoadedAsync(Invoice account)
    {
        var collection = _db.Entry(account).Collection(i => i.Payments);
        if (!collection.IsLoaded)
        {
            await collection.LoadAsync();
        }
    }

    private void CreateInstallments(Invoice account, decimal totalAmount, DateOnly firstInstallmentDate, int installmentCount, int intervalMonths, int firstInstallmentNumber, string note)
    {
        var installmentAmounts = BuildInstallmentAmounts(totalAmount, installmentCount);

        for (var index = 0; index < installmentCount; index++)
        {
            var payment = new Payment
            {
                InvoiceId = account.Id,
                PaymentDate = firstInstallmentDate.AddMonths(index * intervalMonths),
                Amount = installmentAmounts[index],
                PaymentMethod = PaymentMethod.Other,
                Notes = note,
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = firstInstallmentNumber + index,
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            account.Payments.Add(payment);
        }
    }

    private static void RedistributeInstallments(IReadOnlyList<Payment> installments, decimal totalAmount)
    {
        var amounts = BuildInstallmentAmounts(totalAmount, installments.Count);
        for (var index = 0; index < installments.Count; index++)
        {
            installments[index].Amount = amounts[index];
        }
    }

    private static List<decimal> BuildInstallmentAmounts(decimal totalAmount, int installmentCount)
    {
        var amounts = new List<decimal>(installmentCount);
        var baseInstallmentAmount = Math.Round(totalAmount / installmentCount, 2, MidpointRounding.AwayFromZero);
        var runningTotal = 0m;

        for (var installmentNumber = 1; installmentNumber <= installmentCount; installmentNumber++)
        {
            var amount = installmentNumber == installmentCount
                ? totalAmount - runningTotal
                : baseInstallmentAmount;

            runningTotal += amount;
            amounts.Add(amount);
        }

        return amounts;
    }

    private static int GetNextInstallmentNumber(Invoice account)
    {
        return account.Payments
            .Where(p => p.IsPlanned)
            .Select(p => p.InstallmentNumber ?? 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static string AppendNote(string? currentValue, string nextValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return nextValue;
        }

        return $"{currentValue}{Environment.NewLine}{nextValue}";
    }
}
