using System.ComponentModel.DataAnnotations;
using SalesSystem.Api.Domain;

namespace SalesSystem.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ---------- Auth ----------

/// <summary>
/// Either <c>Username</c> (general login page) or <c>Portal</c> (an employee's personal login link) identifies the account.
/// With a portal only the employee that owns the link can sign in.
/// </summary>
public record LoginRequest(
    [MaxLength(150)] string? Username,
    [Required(ErrorMessage = "كلمة المرور مطلوبة"), MaxLength(128)] string Password,
    [MaxLength(32)] string? Portal = null);

/// <summary>Public info shown on an employee's personal login page.</summary>
public record LoginPortalDto(string FullName, string? JobTitle);

public record UserInfoDto(
    int Id, string Username, string FullName, string? Email, string? Phone, string Role, int? EmployeeId, string? EmployeeCode, string? JobTitle,
    DateTime? LastLoginAtUtc);

/// <summary>Fields a user may change on their own account. Name, role, username and deduction rate stay admin-only.</summary>
public record UpdateProfileRequest
{
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح"), MaxLength(256)] public string? Email { get; init; }
    [MaxLength(32), RegularExpression(@"^[0-9+\-\s]*$", ErrorMessage = "رقم الهاتف غير صالح")] public string? Phone { get; init; }
}

public record LoginResponse(string Token, DateTime ExpiresAtUtc, UserInfoDto User);

public record ChangePasswordRequest(
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")] string CurrentPassword,
    [Required, MinLength(8, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف"), MaxLength(128)] string NewPassword);

// ---------- Employees ----------

public record EmployeeListItemDto(
    int Id, int UserId, string EmployeeCode, string FullName, string Username, string? Email, string? Phone, string? JobTitle,
    bool IsActive, decimal DeductionPercentage, int OrdersCount, decimal TotalSales, DateTime? LastLoginAtUtc, DateTime CreatedAtUtc);

public record EmployeeDetailDto(
    int Id, int UserId, string EmployeeCode, string FullName, string Username, string? Email, string? Phone, string? JobTitle,
    DateOnly? HireDate, bool IsActive, decimal DeductionPercentage, DateTime? LastLoginAtUtc, DateTime CreatedAtUtc, string? LoginSlug);

public record CreateEmployeeRequest
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب"), MaxLength(150)] public string FullName { get; init; } = "";
    [Required(ErrorMessage = "اسم المستخدم مطلوب"), RegularExpression(@"^[a-zA-Z0-9._-]{3,64}$", ErrorMessage = "اسم المستخدم يجب أن يتكون من 3-64 حرفاً إنجليزياً أو أرقاماً")]
    public string Username { get; init; } = "";
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح"), MaxLength(256)] public string? Email { get; init; }
    [Required, MinLength(8, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف"), MaxLength(128)] public string Password { get; init; } = "";
    [MaxLength(32)] public string? Phone { get; init; }
    [MaxLength(100)] public string? JobTitle { get; init; }
    public DateOnly? HireDate { get; init; }
    [Range(0, 100, ErrorMessage = "النسبة يجب أن تكون بين 0 و 100")] public decimal DeductionPercentage { get; init; }
}

public record UpdateEmployeeRequest
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب"), MaxLength(150)] public string FullName { get; init; } = "";
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح"), MaxLength(256)] public string? Email { get; init; }
    [MaxLength(32)] public string? Phone { get; init; }
    [MaxLength(100)] public string? JobTitle { get; init; }
    public DateOnly? HireDate { get; init; }
}

public record ResetPasswordRequest([Required, MinLength(8, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف"), MaxLength(128)] string NewPassword);

public record SetStatusRequest(bool IsActive);

public record SetDeductionRequest(
    [Range(0, 100, ErrorMessage = "النسبة يجب أن تكون بين 0 و 100")] decimal Percentage,
    [MaxLength(500)] string? Notes);

public record DeductionRuleDto(int Id, decimal Percentage, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc, string? Notes, string? CreatedBy);

public record EmployeeLookupDto(int Id, string FullName, string EmployeeCode, bool IsActive);

// ---------- Orders ----------

public record OrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "نوع الخدمة مطلوب")] public int ServiceTypeId { get; init; }
    [MaxLength(500)] public string? Description { get; init; }
    [Range(1, 10000, ErrorMessage = "الكمية يجب أن تكون بين 1 و 10000")] public int Quantity { get; init; } = 1;
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")] public decimal UnitPrice { get; init; }
}

public record CreateOrderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "نوع الطلب مطلوب")] public int OrderTypeId { get; init; } = OrderType.SalesId;
    /// <summary>Business date of the operation; defaults to today. Cannot be in the future.</summary>
    public DateOnly? OperationDate { get; init; }
    [Required, MinLength(1, ErrorMessage = "يجب إضافة خدمة واحدة على الأقل"), MaxLength(50)] public List<OrderItemRequest> Items { get; init; } = [];
    [Required] public PaymentMethod PaymentMethod { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
    /// <summary>Only honoured for admins. Employees always create orders for themselves.</summary>
    public int? EmployeeId { get; init; }
}

public record CancelOrderRequest([Required(ErrorMessage = "سبب الإلغاء مطلوب"), MaxLength(500)] string Reason);

public record OrderQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? EmployeeId { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public OrderStatus? Status { get; init; }
    public int? OrderTypeId { get; init; }
    public int? ServiceTypeId { get; init; }
    /// <summary>date (default), number, amount, deduction, net, employee, status.</summary>
    public string? SortBy { get; init; }
    /// <summary>asc or desc (default).</summary>
    public string? SortDir { get; init; }
}

public record OrderListItemDto(
    int Id, string OrderNumber, DateOnly OperationDate, DateTime CreatedAtUtc, int EmployeeId, string EmployeeName, int OrderTypeId, string OrderTypeName,
    OrderStatus Status, PaymentMethod PaymentMethod, string? ServiceName, int ItemsCount, decimal Subtotal, decimal DeductionPercentage,
    decimal DeductionAmount, decimal NetAmount);

public record OrderItemDto(int Id, int ServiceTypeId, string ServiceName, string? Description, int Quantity, decimal UnitPrice, decimal LineTotal);

public record PaymentDto(int Id, decimal Amount, PaymentMethod Method, PaymentStatus Status, DateTime PaidAtUtc, DateTime? VoidedAtUtc);

public record OrderDetailDto(
    int Id, string OrderNumber, DateOnly OperationDate, DateTime CreatedAtUtc, int EmployeeId, string EmployeeName, string EmployeeCode,
    int OrderTypeId, string OrderTypeName, OrderStatus Status, PaymentMethod PaymentMethod, decimal Subtotal, decimal DeductionPercentage, decimal DeductionAmount, decimal NetAmount,
    DeductionTreatment? DeductionTreatment, string? Notes, DateTime? CompletedAtUtc, DateTime? CancelledAtUtc, string? CancellationReason,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<PaymentDto> Payments);

// ---------- Catalog (order types, services, payment methods) ----------

public record OrderTypeDto(int Id, string Code, string Name, bool IsActive, int SortOrder);

public record OrderTypeRequest
{
    [Required(ErrorMessage = "رمز النوع مطلوب"), RegularExpression(@"^[A-Za-z][A-Za-z0-9_-]{1,31}$", ErrorMessage = "الرمز يجب أن يكون إنجليزياً (2-32 حرفاً)")]
    public string Code { get; init; } = "";
    [Required(ErrorMessage = "اسم النوع مطلوب"), MaxLength(100)] public string Name { get; init; } = "";
    public bool IsActive { get; init; } = true;
    [Range(0, 1000)] public int SortOrder { get; init; }
}

public record ServiceTypeDto(int Id, string Name, string? Description, decimal? DefaultPrice, int? OrderTypeId, string? OrderTypeName, bool IsActive, int SortOrder);

public record ServiceTypeRequest
{
    [Required(ErrorMessage = "اسم الخدمة مطلوب"), MaxLength(200)] public string Name { get; init; } = "";
    [MaxLength(500)] public string? Description { get; init; }
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "السعر يجب أن يكون أكبر من صفر")] public decimal? DefaultPrice { get; init; }
    public int? OrderTypeId { get; init; }
    public bool IsActive { get; init; } = true;
    [Range(0, 1000)] public int SortOrder { get; init; }
}

public record PaymentMethodDto(PaymentMethod Id, string Code, string Name, bool IsActive);

public record DeductionInfoDto(int EmployeeId, string EmployeeName, decimal Percentage, bool Enabled, DeductionTreatment Treatment, string Label);

// ---------- Settings ----------

public record DeductionSettingsDto(
    bool Enabled,
    DeductionTreatment Treatment,
    [Required(ErrorMessage = "مسمى الاستقطاع مطلوب"), MaxLength(50)] string Label);

// ---------- Rentals ----------

public record RentalRequest
{
    [Required(ErrorMessage = "اسم العقار مطلوب"), MaxLength(200)] public string Title { get; init; } = "";
    [Required] public RentalPropertyType PropertyType { get; init; } = RentalPropertyType.Property;
    [MaxLength(300)] public string? Location { get; init; }
    [Required(ErrorMessage = "اسم المستفيد مطلوب"), MaxLength(150)] public string BeneficiaryName { get; init; } = "";
    [MaxLength(32)] public string? BeneficiaryPhone { get; init; }
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "قيمة الإيجار يجب أن تكون أكبر من صفر")] public decimal Amount { get; init; }
    [Required(ErrorMessage = "تاريخ الاستحقاق مطلوب")] public DateOnly DueDate { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
}

public record RentalQuery
{
    public string? Search { get; init; }
    public RentalStatus? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool IncludeArchived { get; init; }
}

public record RentalListItemDto(
    int Id, string Title, RentalPropertyType PropertyType, string? Location, string BeneficiaryName, decimal Amount, decimal PaidAmount,
    decimal RemainingAmount, DateOnly DueDate, RentalStatus Status, bool IsArchived, int PaymentsCount);

public record RentalPaymentDto(
    int Id, decimal Amount, PaymentMethod Method, DateOnly PaidOn, string? Reference, string? Notes, bool HasAttachment,
    string? AttachmentName, bool IsVoided, string? VoidReason, DateTime CreatedAtUtc);

public record RentalDetailDto(
    int Id, string Title, RentalPropertyType PropertyType, string? Location, string BeneficiaryName, string? BeneficiaryPhone, decimal Amount,
    decimal PaidAmount, decimal RemainingAmount, DateOnly DueDate, RentalStatus Status, string? Notes, bool IsArchived, DateTime CreatedAtUtc,
    IReadOnlyList<RentalPaymentDto> Payments);

public record RentalPaymentRequest
{
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")] public decimal Amount { get; init; }
    [Required] public PaymentMethod Method { get; init; }
    [Required(ErrorMessage = "تاريخ السداد مطلوب")] public DateOnly PaidOn { get; init; }
    [MaxLength(100)] public string? Reference { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
    public IFormFile? Attachment { get; init; }
}

public record VoidRequest([Required(ErrorMessage = "السبب مطلوب"), MaxLength(500)] string Reason);

public record RentalSummaryDto(int Total, int Upcoming, int Due, int PartiallyPaid, int Paid, int Overdue, decimal TotalAmount, decimal TotalPaid, decimal TotalRemaining, decimal OverdueAmount);

// ---------- Dashboard ----------

public record KeyValueAmount(string Key, decimal Amount, int Count);

/// <summary>Headline totals for one period; used to compare the selected range with the one just before it.</summary>
public record PeriodTotalsDto(DateOnly From, DateOnly To, decimal TotalSales, int OrdersCount, decimal TotalCollected, decimal TotalDeductions, decimal TotalNet);

public record EmployeePerformanceDto(int EmployeeId, string EmployeeName, string EmployeeCode, int OrdersCount, decimal TotalSales, decimal TotalDeductions, decimal TotalNet);

public record AdminDashboardDto(
    decimal TotalSales, int OrdersCount, decimal TotalCollected, decimal TotalDeductions, decimal TotalNet,
    decimal TotalRentalPayments, decimal TotalRentals, decimal RentalsOutstanding, int ActiveEmployees, int PendingOrders, int OverdueRentals,
    PeriodTotalsDto? Previous,
    IReadOnlyList<KeyValueAmount> DailySales, IReadOnlyList<KeyValueAmount> MonthlySales,
    IReadOnlyList<KeyValueAmount> ByEmployee, IReadOnlyList<KeyValueAmount> ByPaymentMethod, IReadOnlyList<KeyValueAmount> ByService,
    IReadOnlyList<EmployeePerformanceDto> EmployeePerformance,
    IReadOnlyList<OrderListItemDto> RecentOrders);

public record EmployeeDashboardDto(
    int TodayOrders, decimal TodaySales, int OrdersCount, decimal TotalSales, decimal TotalCollected, decimal TotalDeductions, decimal TotalNet,
    int PendingOrders, int CompletedOrders, int CancelledOrders, decimal DeductionPercentage, string DeductionLabel,
    IReadOnlyList<KeyValueAmount> DailySales, IReadOnlyList<KeyValueAmount> ByPaymentMethod, IReadOnlyList<OrderListItemDto> RecentOrders);

public record EmployeeStatsDto(int OrdersCount, decimal TotalSales, decimal TotalDeductions, decimal TotalNet, decimal CashTotal, decimal CardTotal, int CancelledCount);

// ---------- Audit ----------

public record AuditLogDto(long Id, DateTime CreatedAtUtc, string? Username, string Action, string EntityType, string? EntityId, string? Details, string? IpAddress);

public record AuditQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Search { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public int? UserId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

// ---------- Reports ----------

public enum ReportType { Sales, Employees, Cash, Card, Deductions, Rentals, Operations, Performance, Comprehensive }

public record ReportQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? EmployeeId { get; init; }
    /// <summary>Performance report bucket: day, week, month (default) or year.</summary>
    public string? GroupBy { get; init; }
    /// <summary>json (default), xlsx or pdf.</summary>
    public string? Format { get; init; }
}

public record ReportColumn(string Key, string Title, string Kind = "text");

public record ReportSummaryItem(string Label, string Value);

public record ReportSection(string Title, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

public record ReportResult(string Title, string Period, DateTime GeneratedAtUtc, IReadOnlyList<ReportSummaryItem> Summary, IReadOnlyList<ReportSection> Sections);
