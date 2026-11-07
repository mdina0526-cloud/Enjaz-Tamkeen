using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Domain;

namespace SalesSystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>For provider-specific subclasses that carry their own migrations (see <see cref="SqliteAppDbContext"/>).</summary>
    // Raw SQL below (index filters, CHECK constraints) quotes identifiers with "..." so it is valid on PostgreSQL and SQLite.
    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDeductionRule> EmployeeDeductionRules => Set<EmployeeDeductionRule>();
    public DbSet<OrderType> OrderTypes => Set<OrderType>();
    public DbSet<ServiceType> Services => Set<ServiceType>();
    public DbSet<PaymentMethodType> PaymentMethods => Set<PaymentMethodType>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DeductionRecord> DeductionRecords => Set<DeductionRecord>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<RentalPayment> RentalPayments => Set<RentalPayment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);

        // SQLite (local demo / tests only) cannot aggregate decimal columns server-side.
        if (Database.IsSqlite())
            configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Role { Id = 1, Name = RoleNames.SuperAdmin, DisplayName = "المدير العام" },
                new Role { Id = 2, Name = RoleNames.Employee, DisplayName = "موظف" });
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.SecurityStamp).HasMaxLength(64);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Employee>(e =>
        {
            e.ToTable("EmployeeProfiles");
            e.Property(x => x.EmployeeCode).HasMaxLength(32);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.JobTitle).HasMaxLength(100);
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.Property(x => x.LoginSlug).HasMaxLength(32);
            e.HasIndex(x => x.LoginSlug).IsUnique().HasFilter("\"LoginSlug\" IS NOT NULL");
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(u => u.Employee).HasForeignKey<Employee>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EmployeeDeductionRule>(e =>
        {
            e.ToTable("EmployeeDeductionRates");
            e.Property(x => x.Percentage).HasPrecision(5, 2);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveToUtc });
            e.HasOne(x => x.Employee).WithMany(x => x.DeductionRules).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
 e.ToTable(t => t.HasCheckConstraint("CK_DeductionRate_Percentage", "\"Percentage\" >= 0 AND \"Percentage\" <= 100"));
        });

        b.Entity<OrderType>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(new OrderType { Id = OrderType.SalesId, Code = "Sales", Name = "مبيعات", SortOrder = 1 });
        });

        b.Entity<ServiceType>(e =>
        {
            e.ToTable("Services", t => t.HasCheckConstraint("CK_Service_DefaultPrice", "\"DefaultPrice\" IS NULL OR \"DefaultPrice\" > 0"));
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.OrderType).WithMany().HasForeignKey(x => x.OrderTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasData(
                new ServiceType { Id = 1, Name = "استشارة", OrderTypeId = OrderType.SalesId, SortOrder = 1 },
                new ServiceType { Id = 2, Name = "تصميم", OrderTypeId = OrderType.SalesId, SortOrder = 2 },
                new ServiceType { Id = 3, Name = "خدمة عامة", SortOrder = 3 });
        });

        b.Entity<PaymentMethodType>(e =>
        {
            e.ToTable("PaymentMethods");
            e.Property(x => x.Id).HasConversion<int>().ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasData(
                new PaymentMethodType { Id = PaymentMethod.Cash, Code = "Cash", Name = "نقداً" },
                new PaymentMethodType { Id = PaymentMethod.Card, Code = "POS", Name = "شبكة / بطاقة (POS)" });
        });

        b.Entity<SalesOrder>(e =>
        {
            e.ToTable("Orders");
            e.Property(x => x.OrderNumber).HasMaxLength(40);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasConversion<int>().HasColumnName("PaymentMethodId");
            e.HasOne(x => x.OrderType).WithMany().HasForeignKey(x => x.OrderTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentMethodType>().WithMany().HasForeignKey(x => x.PaymentMethod).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DeductionPercentage).HasPrecision(5, 2);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.IdempotencyKey).HasMaxLength(64);
            e.Property(x => x.CancellationReason).HasMaxLength(500);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.HasIndex(x => new { x.EmployeeId, x.OperationDate });
            e.HasIndex(x => x.OperationDate);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Employee).WithMany(x => x.SalesOrders).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SalesOrder_Subtotal", "\"Subtotal\" > 0");
                t.HasCheckConstraint("CK_SalesOrder_DeductionPercentage", "\"DeductionPercentage\" >= 0 AND \"DeductionPercentage\" <= 100");
                t.HasCheckConstraint("CK_SalesOrder_DeductionAmount", "\"DeductionAmount\" >= 0 AND \"DeductionAmount\" <= \"Subtotal\"");
            });
        });

        b.Entity<SalesOrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.HasOne(x => x.ServiceType).WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.SalesOrder).WithMany(x => x.Items).HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SalesOrderItem_Quantity", "\"Quantity\" > 0");
                t.HasCheckConstraint("CK_SalesOrderItem_UnitPrice", "\"UnitPrice\" > 0");
            });
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("OrderPayments");
            e.Property(x => x.Method).HasConversion<int>().HasColumnName("PaymentMethodId");
            e.HasOne<PaymentMethodType>().WithMany().HasForeignKey(x => x.Method).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Reference).HasMaxLength(100);
            // At most one active payment per order: a retried request can never double-charge.
            e.HasIndex(x => x.SalesOrderId).IsUnique().HasFilter("\"Status\" = 'Completed'");
            e.HasIndex(x => x.PaidAtUtc);
            e.HasOne(x => x.SalesOrder).WithMany(x => x.Payments).HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "\"Amount\" > 0"));
        });

        b.Entity<DeductionRecord>(e =>
        {
            e.Property(x => x.Percentage).HasPrecision(5, 2);
            e.Property(x => x.Treatment).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.SalesOrderId).IsUnique();
            e.HasIndex(x => new { x.EmployeeId, x.CreatedAtUtc });
            e.HasOne(x => x.SalesOrder).WithOne(x => x.DeductionRecord).HasForeignKey<DeductionRecord>(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DeductionRule).WithMany().HasForeignKey(x => x.DeductionRuleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Rental>(e =>
        {
            e.ToTable("RentalContracts");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.PropertyType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Location).HasMaxLength(300);
            e.Property(x => x.BeneficiaryName).HasMaxLength(150);
            e.Property(x => x.BeneficiaryPhone).HasMaxLength(32);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.IsArchived, x.DueDate });
            e.ToTable(t => t.HasCheckConstraint("CK_RentalContract_Amount", "\"Amount\" > 0"));
        });

        b.Entity<RentalPayment>(e =>
        {
            e.Property(x => x.Method).HasConversion<int>().HasColumnName("PaymentMethodId");
            e.HasOne<PaymentMethodType>().WithMany().HasForeignKey(x => x.Method).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.AttachmentStoredName).HasMaxLength(100);
            e.Property(x => x.AttachmentOriginalName).HasMaxLength(255);
            e.Property(x => x.AttachmentContentType).HasMaxLength(100);
            e.Property(x => x.IdempotencyKey).HasMaxLength(64);
            e.Property(x => x.VoidReason).HasMaxLength(500);
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.HasIndex(x => new { x.RentalId, x.PaidOn });
            e.HasOne(x => x.Rental).WithMany(x => x.Payments).HasForeignKey(x => x.RentalId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_RentalPayment_Amount", "\"Amount\" > 0"));
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.EntityType).HasMaxLength(64);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.Details).HasMaxLength(4000);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.UserId);
        });

        b.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(1000);
        });
    }
}
