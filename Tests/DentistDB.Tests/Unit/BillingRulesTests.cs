using DentistDB.Models;
using DentistDB.Services;
using Xunit;

namespace DentistDB.Tests.Unit;

public class BillingRulesTests
{
    [Fact]
    public void CalculateTotal_SumsQuantityTimesUnitPrice()
    {
        var items = new[]
        {
            new InvoiceItem { Quantity = 2, UnitPrice = 1250.50m },
            new InvoiceItem { Quantity = 1, UnitPrice = 99.99m }
        };

        Assert.Equal(2600.99m, BillingRules.CalculateTotal(items));
    }

    [Fact]
    public void BuildInstallmentPlan_AlwaysSumsToTotal()
    {
        var plan = BillingRules.BuildInstallmentPlan(25000m, new DateOnly(2026, 1, 15), 6, 1);

        Assert.Equal(6, plan.Count);
        Assert.Equal(25000m, plan.Sum(p => p.Amount));
        Assert.Equal(4166.67m, plan[0].Amount);
        Assert.Equal(4166.65m, plan[^1].Amount);
        Assert.All(plan, p => Assert.True(p.IsPlanned && !p.IsSettled));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, plan.Select(p => p.InstallmentNumber!.Value));
    }

    [Fact]
    public void BuildInstallmentPlan_UsesIntervalMonths()
    {
        var plan = BillingRules.BuildInstallmentPlan(900m, new DateOnly(2026, 1, 31), 3, 2);

        Assert.Equal(new DateOnly(2026, 1, 31), plan[0].PaymentDate);
        Assert.Equal(new DateOnly(2026, 3, 31), plan[1].PaymentDate);
        Assert.Equal(new DateOnly(2026, 5, 31), plan[2].PaymentDate);
    }

    [Theory]
    [InlineData(0, InvoiceStatus.Issued)]
    [InlineData(100, InvoiceStatus.PartiallyPaid)]
    [InlineData(500, InvoiceStatus.Paid)]
    [InlineData(600, InvoiceStatus.Paid)]
    public void DeriveStatus_FollowsCollectedAmount(decimal paid, InvoiceStatus expected)
    {
        var invoice = new Invoice { TotalAmount = 500m, Status = InvoiceStatus.Issued };
        if (paid > 0)
        {
            invoice.Payments.Add(new Payment { Amount = paid, IsSettled = true });
        }

        Assert.Equal(expected, BillingRules.DeriveStatus(invoice));
    }

    [Fact]
    public void DeriveStatus_IgnoresUnsettledInstallments()
    {
        var invoice = new Invoice { TotalAmount = 500m, Status = InvoiceStatus.Issued };
        invoice.Payments.Add(new Payment { Amount = 500m, IsPlanned = true, IsSettled = false });

        Assert.Equal(InvoiceStatus.Issued, BillingRules.DeriveStatus(invoice));
        Assert.Equal(500m, invoice.Balance);
    }

    [Fact]
    public void DeriveStatus_KeepsCancelledAndDraft()
    {
        var cancelled = new Invoice { TotalAmount = 100m, Status = InvoiceStatus.Cancelled };
        var draft = new Invoice { TotalAmount = 100m, Status = InvoiceStatus.Draft };

        Assert.Equal(InvoiceStatus.Cancelled, BillingRules.DeriveStatus(cancelled));
        Assert.Equal(InvoiceStatus.Draft, BillingRules.DeriveStatus(draft));
    }
}
