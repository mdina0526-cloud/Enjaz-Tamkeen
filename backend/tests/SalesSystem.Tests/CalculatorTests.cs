using SalesSystem.Api.Domain;
using SalesSystem.Api.Services;

namespace SalesSystem.Tests;

public class CalculatorTests
{
    [Fact]
    public void Deduction_of_5_percent_on_1000_is_50_leaving_950()
    {
        var r = DeductionCalculator.Calculate(1000m, 5m, DeductionTreatment.SubtractFromNet);
        Assert.Equal(1000m, r.Gross);
        Assert.Equal(50m, r.DeductionAmount);
        Assert.Equal(950m, r.NetAmount);
    }

    [Theory]
    [InlineData(1000, 10, 100, 900)]
    [InlineData(1000, 0, 0, 1000)]
    [InlineData(333.33, 7.5, 25.00, 308.33)]   // 24.99975 → 25.00
    [InlineData(0.05, 10, 0.01, 0.04)]          // 0.005 rounds away from zero
    [InlineData(1234.56, 100, 1234.56, 0)]
    public void Deduction_rounds_to_two_decimals(decimal gross, decimal pct, decimal expectedDeduction, decimal expectedNet)
    {
        var r = DeductionCalculator.Calculate(gross, pct, DeductionTreatment.SubtractFromNet);
        Assert.Equal(expectedDeduction, r.DeductionAmount);
        Assert.Equal(expectedNet, r.NetAmount);
        Assert.Equal(r.Gross, r.DeductionAmount + r.NetAmount);
    }

    [Fact]
    public void RecordOnly_keeps_net_equal_to_gross()
    {
        var r = DeductionCalculator.Calculate(1000m, 5m, DeductionTreatment.RecordOnly);
        Assert.Equal(50m, r.DeductionAmount);
        Assert.Equal(1000m, r.NetAmount);
    }

    [Fact]
    public void Disabled_deduction_records_zero()
    {
        var r = DeductionCalculator.Calculate(1000m, 5m, DeductionTreatment.SubtractFromNet, enabled: false);
        Assert.Equal(0m, r.Percentage);
        Assert.Equal(0m, r.DeductionAmount);
        Assert.Equal(1000m, r.NetAmount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Invalid_percentage_is_rejected(decimal pct) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DeductionCalculator.Calculate(100m, pct, DeductionTreatment.SubtractFromNet));

    private static readonly DateOnly Today = new(2026, 10, 27);

    [Theory]
    [InlineData(1000, 1000, 10, RentalStatus.Paid)]
    [InlineData(1000, 1000, -10, RentalStatus.Paid)]
    [InlineData(1000, 0, -1, RentalStatus.Overdue)]
    [InlineData(1000, 400, -1, RentalStatus.Overdue)]
    [InlineData(1000, 400, 5, RentalStatus.PartiallyPaid)]
    [InlineData(1000, 0, 0, RentalStatus.Due)]
    [InlineData(1000, 0, 7, RentalStatus.Due)]
    [InlineData(1000, 0, 8, RentalStatus.Upcoming)]
    public void Rental_status(decimal amount, decimal paid, int dueInDays, RentalStatus expected) =>
        Assert.Equal(expected, RentalStatusCalculator.Compute(amount, paid, Today.AddDays(dueInDays), Today));
}
