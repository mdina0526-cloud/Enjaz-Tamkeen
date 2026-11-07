using SalesSystem.Api.Domain;

namespace SalesSystem.Api.Services;

public readonly record struct DeductionResult(decimal Gross, decimal Percentage, decimal DeductionAmount, decimal NetAmount);

public static class DeductionCalculator
{
    /// <summary>
    /// Deduction = round(gross × pct / 100, 2, away-from-zero). With <see cref="DeductionTreatment.RecordOnly"/>
    /// the amount is still recorded but the net amount stays equal to the gross.
    /// </summary>
    public static DeductionResult Calculate(decimal gross, decimal percentage, DeductionTreatment treatment, bool enabled = true)
    {
        if (gross < 0) throw new ArgumentOutOfRangeException(nameof(gross));
        if (percentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentage));

        gross = Math.Round(gross, 2, MidpointRounding.AwayFromZero);
        if (!enabled) return new DeductionResult(gross, 0, 0, gross);

        var deduction = Math.Round(gross * percentage / 100m, 2, MidpointRounding.AwayFromZero);
        var net = treatment == DeductionTreatment.SubtractFromNet ? gross - deduction : gross;
        return new DeductionResult(gross, percentage, deduction, net);
    }

    public static decimal LineTotal(int quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
}

public static class RentalStatusCalculator
{
    public const int DueSoonDays = 7;

    /// <summary>
    /// Paid → fully covered. Past due date and not fully paid → Overdue (even if partially paid).
    /// Some payment before due date → PartiallyPaid. Within <see cref="DueSoonDays"/> → Due. Otherwise Upcoming.
    /// </summary>
    public static RentalStatus Compute(decimal amount, decimal paid, DateOnly dueDate, DateOnly today)
    {
        if (paid >= amount) return RentalStatus.Paid;
        if (dueDate < today) return RentalStatus.Overdue;
        if (paid > 0) return RentalStatus.PartiallyPaid;
        if (dueDate <= today.AddDays(DueSoonDays)) return RentalStatus.Due;
        return RentalStatus.Upcoming;
    }
}
