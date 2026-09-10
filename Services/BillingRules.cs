using DentistDB.Models;

namespace DentistDB.Services;

/// <summary>Pure business rules for invoices: totals, installment plans and status derivation.</summary>
public static class BillingRules
{
    public static decimal CalculateTotal(IEnumerable<InvoiceItem> items)
    {
        return Math.Round(items.Sum(i => i.Quantity * i.UnitPrice), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Splits <paramref name="total"/> into <paramref name="installmentCount"/> planned payments.
    /// Rounding differences are absorbed by the last installment so the plan always sums to the total.
    /// </summary>
    public static List<Payment> BuildInstallmentPlan(decimal total, DateOnly firstPaymentDate, int installmentCount, int intervalMonths)
    {
        if (installmentCount < 1) throw new ArgumentOutOfRangeException(nameof(installmentCount));
        if (intervalMonths < 1) throw new ArgumentOutOfRangeException(nameof(intervalMonths));

        var installmentAmount = Math.Round(total / installmentCount, 2, MidpointRounding.AwayFromZero);
        var runningTotal = 0m;
        var plan = new List<Payment>(installmentCount);

        for (var i = 1; i <= installmentCount; i++)
        {
            var amount = i == installmentCount ? total - runningTotal : installmentAmount;
            runningTotal += amount;

            plan.Add(new Payment
            {
                PaymentDate = firstPaymentDate.AddMonths((i - 1) * intervalMonths),
                Amount = amount,
                PaymentMethod = PaymentMethod.Other,
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = i,
                CreatedAt = DateTime.UtcNow
            });
        }

        return plan;
    }

    /// <summary>Derives the invoice status from what has actually been collected. Cancelled and Draft are sticky.</summary>
    public static InvoiceStatus DeriveStatus(Invoice invoice)
    {
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Draft)
        {
            return invoice.Status;
        }

        var paid = invoice.TotalPaid;

        if (invoice.TotalAmount > 0 && paid >= invoice.TotalAmount)
        {
            return InvoiceStatus.Paid;
        }

        if (paid > 0)
        {
            return InvoiceStatus.PartiallyPaid;
        }

        return InvoiceStatus.Issued;
    }
}
