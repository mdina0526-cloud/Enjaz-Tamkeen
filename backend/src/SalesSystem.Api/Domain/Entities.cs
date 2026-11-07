namespace SalesSystem.Api.Domain;

public class Role
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public List<User> Users { get; set; } = [];
}

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }
    public required string FullName { get; set; }
    public string PasswordHash { get; set; } = "";
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    /// <summary>Changes whenever credentials or status change; tokens carrying an old stamp are rejected.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Employee? Employee { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string EmployeeCode { get; set; }
    /// <summary>Unguessable token in the employee's personal login link (/e/{slug}). Regenerating it revokes the old link.</summary>
    public string? LoginSlug { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly? HireDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<EmployeeDeductionRule> DeductionRules { get; set; } = [];
    public List<SalesOrder> SalesOrders { get; set; } = [];
}

/// <summary>Deduction percentage history per employee. Exactly one rule is current (EffectiveToUtc == null).</summary>
public class EmployeeDeductionRule
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public decimal Percentage { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? Notes { get; set; }
}

public class SalesOrder
{
    public int Id { get; set; }
    public required string OrderNumber { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int OrderTypeId { get; set; } = OrderType.SalesId;
    public OrderType OrderType { get; set; } = null!;
    /// <summary>Business date of the operation (local calendar). Drives filters, dashboards and reports.</summary>
    public DateOnly OperationDate { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    // Snapshot of the deduction at creation time; later rule changes never touch these.
    public decimal DeductionPercentage { get; set; }
    public decimal DeductionAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public List<SalesOrderItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public DeductionRecord? DeductionRecord { get; set; }
}

/// <summary>Extensible order categories (Sales today; more can be added from settings without code changes).</summary>
public class OrderType
{
    public const int SalesId = 1;

    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>Service catalog used when creating orders.</summary>
public class ServiceType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    /// <summary>Optional suggested price; employees may still enter the actual amount.</summary>
    public decimal? DefaultPrice { get; set; }
    /// <summary>When set, the service may only be used with this order type.</summary>
    public int? OrderTypeId { get; set; }
    public OrderType? OrderType { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class PaymentMethodType
{
    public PaymentMethod Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SalesOrderItem
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public int ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;
    /// <summary>Service name snapshot at creation time, so renaming a catalog entry never rewrites history.</summary>
    public required string ServiceName { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    public string? Reference { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? VoidedAtUtc { get; set; }
}

public class DeductionRecord
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? DeductionRuleId { get; set; }
    public EmployeeDeductionRule? DeductionRule { get; set; }
    public decimal Percentage { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal DeductionAmount { get; set; }
    public decimal NetAmount { get; set; }
    public DeductionTreatment Treatment { get; set; }
    public bool IsVoided { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Rental
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public RentalPropertyType PropertyType { get; set; }
    public string? Location { get; set; }
    public required string BeneficiaryName { get; set; }
    public string? BeneficiaryPhone { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    /// <summary>Bumped inside each payment transaction to take a row lock and serialize concurrent payments.</summary>
    public int LockVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public List<RentalPayment> Payments { get; set; } = [];
}

public class RentalPayment
{
    public int Id { get; set; }
    public int RentalId { get; set; }
    public Rental Rental { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateOnly PaidOn { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentStoredName { get; set; }
    public string? AttachmentOriginalName { get; set; }
    public string? AttachmentContentType { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SystemSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
