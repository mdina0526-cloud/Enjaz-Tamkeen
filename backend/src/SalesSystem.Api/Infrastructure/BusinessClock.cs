using Microsoft.Extensions.Options;

namespace SalesSystem.Api.Infrastructure;

/// <summary>Converts between UTC storage and the business-local calendar.</summary>
public class BusinessClock(TimeProvider time, IOptions<AppOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow));

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);

    public DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _zone);

    /// <summary>Inclusive local date range → half-open UTC range [from, to).</summary>
    public (DateTime FromUtc, DateTime ToUtc) UtcRange(DateOnly? from, DateOnly? to)
    {
        var f = from.HasValue ? StartOfDayUtc(from.Value) : DateTime.MinValue;
        var t = to.HasValue ? StartOfDayUtc(to.Value.AddDays(1)) : DateTime.MaxValue;
        return (f, t);
    }
}
