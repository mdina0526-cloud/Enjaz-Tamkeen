namespace SalesSystem.Api.Domain;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Employee = "Employee";
    public const string OperationsManager = "OperationsManager";
    public const string Accountant = "Accountant";
    public const string OrderStaff = $"{SuperAdmin},{OperationsManager},{Employee}";
    public const string Management = $"{SuperAdmin},{OperationsManager}";
    public const string Finance = $"{SuperAdmin},{Accountant}";
    public const string RentalStaff = $"{SuperAdmin},{OperationsManager},{Accountant}";
}

public enum OrderStatus
{
    /// <summary>Created by an employee, awaiting admin review.</summary>
    Pending = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>Values equal the primary keys of the PaymentMethods lookup table (FK-enforced in the database).</summary>
public enum PaymentMethod
{
    Cash = 1,
    /// <summary>Network / card terminal (POS).</summary>
    Card = 2,
}

public enum PaymentStatus
{
    Completed = 1,
    Voided = 2,
}

public enum RentalPropertyType
{
    Property = 1,
    Office = 2,
    Site = 3,
    Other = 4,
}

public enum RentalStatus
{
    Upcoming = 1,
    Due = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Overdue = 5,
}

/// <summary>
/// How a recorded deduction affects the order's net amount. Configured in system settings
/// so the business decides what a "deduction" means (commission, fee, withholding...).
/// </summary>
public enum DeductionTreatment
{
    /// <summary>Net amount = gross - deduction.</summary>
    SubtractFromNet = 1,
    /// <summary>Deduction is recorded for tracking only; net amount equals gross.</summary>
    RecordOnly = 2,
}
