using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public static class Labels
{
    public static string Of(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "قيد المراجعة",
        OrderStatus.Completed => "مكتمل",
        OrderStatus.Cancelled => "ملغي",
        _ => s.ToString(),
    };

    public static string Of(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "نقداً",
        PaymentMethod.Card => "شبكة / بطاقة",
        _ => m.ToString(),
    };

    public static string Of(RentalStatus s) => s switch
    {
        RentalStatus.Upcoming => "قادم",
        RentalStatus.Due => "مستحق",
        RentalStatus.PartiallyPaid => "مدفوع جزئياً",
        RentalStatus.Paid => "مدفوع",
        RentalStatus.Overdue => "متأخر",
        _ => s.ToString(),
    };

    public static string Of(DeductionTreatment t) => t switch
    {
        DeductionTreatment.SubtractFromNet => "يُطرح من الصافي",
        DeductionTreatment.RecordOnly => "للتسجيل فقط",
        _ => t.ToString(),
    };

    public static string Of(RentalPropertyType t) => t switch
    {
        RentalPropertyType.Property => "عقار",
        RentalPropertyType.Office => "مكتب",
        RentalPropertyType.Site => "موقع",
        _ => "أخرى",
    };
}

public class ReportService(AppDbContext db, BusinessClock clock, RentalService rentals, SettingsService settings)
{
    private const int MaxRows = 20000;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Money(decimal v) => v.ToString("N2", Inv);
    /// <summary>Left-to-right marks keep dates intact when embedded in Arabic sentences.</summary>
    public static string Ltr(string s) => $"\u200E{s}\u200E";
    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", Inv);

    public static string Title(ReportType type) => type switch
    {
        ReportType.Sales => "تقرير المبيعات",
        ReportType.Employees => "تقرير عمليات الموظفين",
        ReportType.Cash => "تقرير المدفوعات النقدية",
        ReportType.Card => "تقرير مدفوعات الشبكة",
        ReportType.Deductions => "تقرير الاستقطاعات",
        ReportType.Rentals => "تقرير الإيجارات",
        ReportType.Operations => "تقرير إجمالي العمليات",
        ReportType.Performance => "تقرير الأداء خلال الفترة",
        ReportType.Comprehensive => "التقرير الشامل",
        _ => type.ToString(),
    };

    public async Task<ReportResult> BuildAsync(ReportType type, ReportQuery q, CancellationToken ct)
    {
        if (q.From > q.To) throw AppException.BadRequest("تاريخ البداية يجب أن يسبق تاريخ النهاية");
        var period = q.From is null && q.To is null
            ? "كل الفترات"
            : $"من {(q.From is { } f ? Ltr(f.ToString("yyyy-MM-dd", Inv)) : "البداية")} إلى {(q.To is { } t ? Ltr(t.ToString("yyyy-MM-dd", Inv)) : "اليوم")}";

        var (summary, sections) = type switch
        {
            ReportType.Sales => await OrdersReport(q, null, ct),
            ReportType.Cash => await OrdersReport(q, PaymentMethod.Cash, ct),
            ReportType.Card => await OrdersReport(q, PaymentMethod.Card, ct),
            ReportType.Employees => await EmployeesReport(q, ct),
            ReportType.Deductions => await DeductionsReport(q, ct),
            ReportType.Rentals => await RentalsReport(q, ct),
            ReportType.Operations => await OperationsReport(q, ct),
            ReportType.Performance => await PerformanceReport(q, ct),
            ReportType.Comprehensive => await ComprehensiveReport(q, ct),
            _ => throw AppException.BadRequest("نوع التقرير غير معروف"),
        };
        return new ReportResult(Title(type), period, clock.UtcNow, summary, sections);
    }

    private IQueryable<SalesOrder> Orders(ReportQuery q)
    {
        var orders = db.SalesOrders.AsNoTracking();
        if (q.From.HasValue) orders = orders.Where(o => o.OperationDate >= q.From.Value);
        if (q.To.HasValue) orders = orders.Where(o => o.OperationDate <= q.To.Value);
        if (q.EmployeeId.HasValue) orders = orders.Where(o => o.EmployeeId == q.EmployeeId);
        return orders;
    }

    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> OrdersReport(ReportQuery q, PaymentMethod? method, CancellationToken ct)
    {
        var orders = Orders(q);
        if (method.HasValue) orders = orders.Where(o => o.PaymentMethod == method.Value);
        var rows = await orders.OrderBy(o => o.OperationDate).ThenBy(o => o.Id).Take(MaxRows)
            .Select(o => new { o.OrderNumber, o.OperationDate, Employee = o.Employee.User.FullName, o.Status, o.PaymentMethod, Items = o.Items.Count, o.Subtotal, o.DeductionAmount, o.NetAmount })
            .ToListAsync(ct);
        var active = rows.Where(r => r.Status != OrderStatus.Cancelled).ToList();
        var label = (await settings.GetDeductionSettingsAsync(ct)).Label;

        var summary = new List<ReportSummaryItem>
        {
            new("عدد العمليات", active.Count.ToString(Inv)),
            new("إجمالي المبيعات", Money(active.Sum(r => r.Subtotal))),
            new($"إجمالي {label}", Money(active.Sum(r => r.DeductionAmount))),
            new("الصافي", Money(active.Sum(r => r.NetAmount))),
            new("العمليات الملغاة", rows.Count(r => r.Status == OrderStatus.Cancelled).ToString(Inv)),
        };
        var section = new ReportSection("العمليات",
            [new("number", "رقم الطلب"), new("date", "التاريخ"), new("employee", "الموظف"), new("status", "الحالة"), new("method", "طريقة الدفع"),
             new("items", "الخدمات", "number"), new("subtotal", "المبلغ", "money"), new("deduction", label, "money"), new("net", "الصافي", "money")],
            rows.Select(r => (IReadOnlyList<object?>)[r.OrderNumber, Day(r.OperationDate), r.Employee, Labels.Of(r.Status), Labels.Of(r.PaymentMethod),
                r.Items, r.Subtotal, r.DeductionAmount, r.NetAmount]).ToList());
        return (summary, [section]);
    }

    private async Task<List<IReadOnlyList<object?>>> EmployeeRows(ReportQuery q, CancellationToken ct)
    {
        var orders = Orders(q);
        var stats = await orders.GroupBy(o => o.EmployeeId).Select(g => new
        {
            EmployeeId = g.Key,
            Count = g.Count(o => o.Status != OrderStatus.Cancelled),
            Sales = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.Subtotal) ?? 0,
            Cash = g.Where(o => o.Status != OrderStatus.Cancelled && o.PaymentMethod == PaymentMethod.Cash).Sum(o => (decimal?)o.Subtotal) ?? 0,
            Card = g.Where(o => o.Status != OrderStatus.Cancelled && o.PaymentMethod == PaymentMethod.Card).Sum(o => (decimal?)o.Subtotal) ?? 0,
            Deductions = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.DeductionAmount) ?? 0,
            Net = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.NetAmount) ?? 0,
            Cancelled = g.Count(o => o.Status == OrderStatus.Cancelled),
        }).ToListAsync(ct);

        var employees = db.Employees.AsNoTracking();
        if (q.EmployeeId.HasValue) employees = employees.Where(e => e.Id == q.EmployeeId);
        var list = await employees.OrderBy(e => e.EmployeeCode).Select(e => new { e.Id, e.EmployeeCode, e.User.FullName, e.User.IsActive }).ToListAsync(ct);

        return list.Select(e =>
        {
            var s = stats.FirstOrDefault(x => x.EmployeeId == e.Id);
            return (IReadOnlyList<object?>)[e.EmployeeCode, e.FullName, e.IsActive ? "نشط" : "معطل", s?.Count ?? 0, s?.Sales ?? 0m, s?.Cash ?? 0m,
                s?.Card ?? 0m, s?.Deductions ?? 0m, s?.Net ?? 0m, s?.Cancelled ?? 0];
        }).ToList();
    }

    private static readonly ReportColumn[] EmployeeColumns =
    [
        new("code", "الرقم الوظيفي"), new("name", "الموظف"), new("state", "الحالة"), new("count", "العمليات", "number"), new("sales", "المبيعات", "money"),
        new("cash", "نقداً", "money"), new("card", "شبكة", "money"), new("deductions", "الاستقطاعات", "money"), new("net", "الصافي", "money"),
        new("cancelled", "الملغاة", "number"),
    ];

    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> EmployeesReport(ReportQuery q, CancellationToken ct)
    {
        var rows = await EmployeeRows(q, ct);
        var summary = new List<ReportSummaryItem>
        {
            new("عدد الموظفين", rows.Count.ToString(Inv)),
            new("إجمالي العمليات", rows.Sum(r => (int)r[3]!).ToString(Inv)),
            new("إجمالي المبيعات", Money(rows.Sum(r => (decimal)r[4]!))),
            new("إجمالي الاستقطاعات", Money(rows.Sum(r => (decimal)r[7]!))),
        };
        return (summary, [new ReportSection("أداء الموظفين", EmployeeColumns, rows)]);
    }

    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> DeductionsReport(ReportQuery q, CancellationToken ct)
    {
        var records = db.DeductionRecords.AsNoTracking().Where(d => !d.IsVoided);
        if (q.From.HasValue) records = records.Where(d => d.SalesOrder.OperationDate >= q.From.Value);
        if (q.To.HasValue) records = records.Where(d => d.SalesOrder.OperationDate <= q.To.Value);
        if (q.EmployeeId.HasValue) records = records.Where(d => d.EmployeeId == q.EmployeeId);
        var rows = await records.OrderBy(d => d.SalesOrder.OperationDate).ThenBy(d => d.Id).Take(MaxRows)
            .Select(d => new { d.SalesOrder.OrderNumber, d.SalesOrder.OperationDate, Employee = d.Employee.User.FullName, d.Percentage, d.BaseAmount, d.DeductionAmount, d.NetAmount, d.Treatment })
            .ToListAsync(ct);
        var label = (await settings.GetDeductionSettingsAsync(ct)).Label;

        var summary = new List<ReportSummaryItem>
        {
            new("عدد السجلات", rows.Count.ToString(Inv)),
            new("إجمالي المبالغ الأساسية", Money(rows.Sum(r => r.BaseAmount))),
            new($"إجمالي {label}", Money(rows.Sum(r => r.DeductionAmount))),
            new("إجمالي الصافي", Money(rows.Sum(r => r.NetAmount))),
        };
        var byEmployee = rows.GroupBy(r => r.Employee).OrderByDescending(g => g.Sum(x => x.DeductionAmount))
            .Select(g => (IReadOnlyList<object?>)[g.Key, g.Count(), g.Sum(x => x.BaseAmount), g.Sum(x => x.DeductionAmount), g.Sum(x => x.NetAmount)]).ToList();

        return (summary,
        [
            new ReportSection($"{label} حسب الموظف",
                [new("employee", "الموظف"), new("count", "العمليات", "number"), new("base", "المبلغ الأساسي", "money"), new("deduction", label, "money"), new("net", "الصافي", "money")],
                byEmployee),
            new ReportSection("التفاصيل",
                [new("number", "رقم الطلب"), new("date", "التاريخ"), new("employee", "الموظف"), new("pct", "النسبة %", "number"), new("base", "المبلغ الأساسي", "money"),
                 new("deduction", label, "money"), new("net", "الصافي", "money"), new("treatment", "طريقة التطبيق")],
                rows.Select(r => (IReadOnlyList<object?>)[r.OrderNumber, Day(r.OperationDate), r.Employee, r.Percentage, r.BaseAmount, r.DeductionAmount, r.NetAmount, Labels.Of(r.Treatment)]).ToList()),
        ]);
    }

    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> RentalsReport(ReportQuery q, CancellationToken ct)
    {
        var list = await rentals.ListAsync(new RentalQuery { From = q.From, To = q.To }, ct);
        var payments = db.RentalPayments.AsNoTracking().Where(p => !p.IsVoided);
        if (q.From.HasValue) payments = payments.Where(p => p.PaidOn >= q.From.Value);
        if (q.To.HasValue) payments = payments.Where(p => p.PaidOn <= q.To.Value);
        var paymentRows = await payments.OrderBy(p => p.PaidOn).Take(MaxRows)
            .Select(p => new { p.PaidOn, p.Rental.Title, p.Rental.BeneficiaryName, p.Amount, p.Method, p.Reference }).ToListAsync(ct);

        var summary = new List<ReportSummaryItem>
        {
            new("عدد الإيجارات", list.Count.ToString(Inv)),
            new("إجمالي قيمة الإيجارات", Money(list.Sum(r => r.Amount))),
            new("المدفوع", Money(list.Sum(r => r.PaidAmount))),
            new("المتبقي", Money(list.Sum(r => r.RemainingAmount))),
            new("المتأخرة", list.Count(r => r.Status == RentalStatus.Overdue).ToString(Inv)),
            new("دفعات الفترة", Money(paymentRows.Sum(p => p.Amount))),
        };
        return (summary,
        [
            new ReportSection("الإيجارات",
                [new("title", "العقار"), new("type", "النوع"), new("beneficiary", "المستفيد"), new("due", "تاريخ الاستحقاق"), new("amount", "القيمة", "money"),
                 new("paid", "المدفوع", "money"), new("remaining", "المتبقي", "money"), new("status", "الحالة")],
                list.Select(r => (IReadOnlyList<object?>)[r.Title, Labels.Of(r.PropertyType), r.BeneficiaryName, r.DueDate.ToString("yyyy-MM-dd", Inv), r.Amount, r.PaidAmount, r.RemainingAmount, Labels.Of(r.Status)]).ToList()),
            new ReportSection("سجل الدفعات",
                [new("date", "تاريخ السداد"), new("title", "العقار"), new("beneficiary", "المستفيد"), new("amount", "المبلغ", "money"), new("method", "طريقة الدفع"), new("ref", "المرجع")],
                paymentRows.Select(p => (IReadOnlyList<object?>)[p.PaidOn.ToString("yyyy-MM-dd", Inv), p.Title, p.BeneficiaryName, p.Amount, Labels.Of(p.Method), p.Reference]).ToList()),
        ]);
    }

    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> ComprehensiveReport(ReportQuery q, CancellationToken ct)
    {
        var active = Orders(q).Where(o => o.Status != OrderStatus.Cancelled);
        var orders = await active.Select(o => new { o.OperationDate, o.Subtotal, o.DeductionAmount, o.NetAmount, o.PaymentMethod }).ToListAsync(ct);
        var (rentalSummary, rentalSections) = await RentalsReport(q with { EmployeeId = null }, ct);
        var rentalPaid = rentalSections[1].Rows.Sum(r => (decimal)r[3]!);

        var summary = new List<ReportSummaryItem>
        {
            new("عدد العمليات", orders.Count.ToString(Inv)),
            new("إجمالي المبيعات", Money(orders.Sum(o => o.Subtotal))),
            new("المدفوعات النقدية", Money(orders.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.Subtotal))),
            new("مدفوعات الشبكة", Money(orders.Where(o => o.PaymentMethod == PaymentMethod.Card).Sum(o => o.Subtotal))),
            new("إجمالي الاستقطاعات", Money(orders.Sum(o => o.DeductionAmount))),
            new("صافي المبيعات", Money(orders.Sum(o => o.NetAmount))),
            new("مدفوعات الإيجارات", Money(rentalPaid)),
            new("الصافي بعد الإيجارات", Money(orders.Sum(o => o.NetAmount) - rentalPaid)),
        };

        var daily = orders.GroupBy(o => o.OperationDate).OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<object?>)[g.Key.ToString("yyyy-MM-dd", Inv), g.Count(),
                g.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.Subtotal), g.Where(o => o.PaymentMethod == PaymentMethod.Card).Sum(o => o.Subtotal),
                g.Sum(o => o.Subtotal), g.Sum(o => o.DeductionAmount), g.Sum(o => o.NetAmount)]).ToList();

        return (summary,
        [
            new ReportSection("الحركة اليومية",
                [new("date", "اليوم"), new("count", "العمليات", "number"), new("cash", "نقداً", "money"), new("card", "شبكة", "money"),
                 new("total", "الإجمالي", "money"), new("deduction", "الاستقطاعات", "money"), new("net", "الصافي", "money")],
                daily),
            new ReportSection("أداء الموظفين", EmployeeColumns, await EmployeeRows(q, ct)),
            new ReportSection("ملخص الإيجارات", [new("label", "البند"), new("value", "القيمة")],
                rentalSummary.Select(s => (IReadOnlyList<object?>)[s.Label, s.Value]).ToList()),
        ]);
    }

    /// <summary>Every operation in the period, including cancelled ones, with its full financial breakdown.</summary>
    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> OperationsReport(ReportQuery q, CancellationToken ct)
    {
        var rows = await Orders(q).OrderBy(o => o.OperationDate).ThenBy(o => o.Id).Take(MaxRows)
            .Select(o => new
            {
                o.OrderNumber, o.OperationDate, Employee = o.Employee.User.FullName, Type = o.OrderType.Name,
                Service = o.Items.OrderBy(i => i.Id).Select(i => i.ServiceName).FirstOrDefault(), Services = o.Items.Count,
                o.Status, o.PaymentMethod, o.Subtotal, o.DeductionPercentage, o.DeductionAmount, o.NetAmount,
            })
            .ToListAsync(ct);
        var label = (await settings.GetDeductionSettingsAsync(ct)).Label;
        var active = rows.Where(r => r.Status != OrderStatus.Cancelled).ToList();

        var summary = new List<ReportSummaryItem>
        {
            new("إجمالي العمليات", rows.Count.ToString(Inv)),
            new("مكتملة", rows.Count(r => r.Status == OrderStatus.Completed).ToString(Inv)),
            new("قيد المراجعة", rows.Count(r => r.Status == OrderStatus.Pending).ToString(Inv)),
            new("ملغاة", rows.Count(r => r.Status == OrderStatus.Cancelled).ToString(Inv)),
            new("إجمالي المبالغ (غير الملغاة)", Money(active.Sum(r => r.Subtotal))),
            new($"إجمالي {label}", Money(active.Sum(r => r.DeductionAmount))),
            new("الصافي", Money(active.Sum(r => r.NetAmount))),
        };

        var byType = active.GroupBy(r => r.Type).OrderByDescending(g => g.Sum(r => r.Subtotal))
            .Select(g => (IReadOnlyList<object?>)[g.Key, g.Count(), g.Sum(r => r.Subtotal), g.Sum(r => r.DeductionAmount), g.Sum(r => r.NetAmount)]).ToList();

        return (summary,
        [
            new ReportSection("حسب نوع العملية",
                [new("type", "نوع العملية"), new("count", "العدد", "number"), new("amount", "المبلغ", "money"), new("deduction", label, "money"), new("net", "الصافي", "money")],
                byType),
            new ReportSection("تفاصيل العمليات",
                [new("number", "رقم العملية"), new("date", "التاريخ"), new("employee", "الموظف"), new("type", "النوع"), new("service", "الخدمة"),
                 new("method", "طريقة الدفع"), new("amount", "المبلغ", "money"), new("pct", "النسبة %", "number"), new("deduction", label, "money"),
                 new("net", "الصافي", "money"), new("status", "الحالة")],
                rows.Select(r => (IReadOnlyList<object?>)[r.OrderNumber, Day(r.OperationDate), r.Employee, r.Type,
                    r.Services > 1 ? $"{r.Service} (+{r.Services - 1})" : r.Service, Labels.Of(r.PaymentMethod), r.Subtotal, r.DeductionPercentage,
                    r.DeductionAmount, r.NetAmount, Labels.Of(r.Status)]).ToList()),
        ]);
    }

    /// <summary>Sales grouped into day/week/month/year buckets with the change against the previous bucket.</summary>
    private async Task<(List<ReportSummaryItem>, List<ReportSection>)> PerformanceReport(ReportQuery q, CancellationToken ct)
    {
        var groupBy = (q.GroupBy ?? "month").ToLowerInvariant();
        if (groupBy is not ("day" or "week" or "month" or "year")) throw AppException.BadRequest("التجميع يجب أن يكون: day, week, month, year");

        var orders = await Orders(q).Where(o => o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.OperationDate, o.Subtotal, o.DeductionAmount, o.NetAmount, o.PaymentMethod, o.EmployeeId })
            .ToListAsync(ct);

        string Bucket(DateOnly d) => groupBy switch
        {
            "day" => Day(d),
            // Weeks start on Saturday, the start of the business week in Saudi Arabia.
            "week" => Day(d.AddDays(-(((int)d.DayOfWeek + 1) % 7))),
            "year" => d.Year.ToString(Inv),
            _ => d.ToString("yyyy-MM", Inv),
        };

        var buckets = orders.GroupBy(o => Bucket(o.OperationDate)).OrderBy(g => g.Key).Select(g => new
        {
            g.Key,
            Count = g.Count(),
            Sales = g.Sum(o => o.Subtotal),
            Cash = g.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.Subtotal),
            Card = g.Where(o => o.PaymentMethod == PaymentMethod.Card).Sum(o => o.Subtotal),
            Deductions = g.Sum(o => o.DeductionAmount),
            Net = g.Sum(o => o.NetAmount),
            Employees = g.Select(o => o.EmployeeId).Distinct().Count(),
        }).ToList();

        var rows = new List<IReadOnlyList<object?>>();
        decimal? prev = null;
        foreach (var b in buckets)
        {
            var change = prev is > 0 ? Math.Round((b.Sales - prev.Value) / prev.Value * 100m, 1, MidpointRounding.AwayFromZero) : (decimal?)null;
            rows.Add([b.Key, b.Count, b.Employees, b.Cash, b.Card, b.Sales, b.Deductions, b.Net,
                b.Count > 0 ? Math.Round(b.Sales / b.Count, 2, MidpointRounding.AwayFromZero) : 0m,
                change is null ? "—" : $"{(change >= 0 ? "+" : "")}{change.Value.ToString("0.0", Inv)}%"]);
            prev = b.Sales;
        }

        var total = orders.Sum(o => o.Subtotal);
        var best = buckets.OrderByDescending(b => b.Sales).FirstOrDefault();
        var groupLabel = groupBy switch { "day" => "يوم", "week" => "أسبوع", "year" => "سنة", _ => "شهر" };
        var summary = new List<ReportSummaryItem>
        {
            new("التجميع حسب", groupLabel),
            new("عدد الفترات", buckets.Count.ToString(Inv)),
            new("عدد العمليات", orders.Count.ToString(Inv)),
            new("إجمالي المبيعات", Money(total)),
            new("متوسط المبيعات لكل فترة", Money(buckets.Count > 0 ? Math.Round(total / buckets.Count, 2, MidpointRounding.AwayFromZero) : 0)),
            new("أفضل فترة", best is null ? "—" : $"{Ltr(best.Key)} ({Money(best.Sales)})"),
        };

        return (summary,
        [
            new ReportSection($"الأداء حسب ال{groupLabel}",
                [new("period", "الفترة"), new("count", "العمليات", "number"), new("employees", "الموظفون", "number"), new("cash", "نقداً", "money"),
                 new("card", "شبكة", "money"), new("sales", "المبيعات", "money"), new("deduction", "الاستقطاعات", "money"), new("net", "الصافي", "money"),
                 new("avg", "متوسط العملية", "money"), new("change", "التغير عن الفترة السابقة")],
                rows),
        ]);
    }
}
