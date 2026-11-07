using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

/// <summary>Lookup data used by order forms: order types, the service catalog and payment methods.</summary>
public class CatalogService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<OrderTypeDto>> OrderTypesAsync(bool includeInactive, CancellationToken ct) =>
        await db.OrderTypes.AsNoTracking()
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new OrderTypeDto(t.Id, t.Code, t.Name, t.IsActive, t.SortOrder))
            .ToListAsync(ct);

    public async Task<OrderTypeDto> CreateOrderTypeAsync(OrderTypeRequest req, CancellationToken ct)
    {
        var entity = new OrderType { Code = req.Code.Trim(), Name = req.Name.Trim() };
        Apply(entity, req);
        await EnsureUniqueOrderTypeAsync(entity, ct);
        db.OrderTypes.Add(entity);
        await db.SaveChangesAsync(ct);
        audit.Add(AuditActions.Create, "OrderType", entity.Id, new { entity.Code, entity.Name });
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<OrderTypeDto> UpdateOrderTypeAsync(int id, OrderTypeRequest req, CancellationToken ct)
    {
        var entity = await db.OrderTypes.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw AppException.NotFound("نوع الطلب غير موجود");
        var before = ToDto(entity);
        Apply(entity, req);
        if (!entity.IsActive && !await db.OrderTypes.AnyAsync(t => t.Id != id && t.IsActive, ct))
            throw AppException.BadRequest("يجب أن يبقى نوع طلب واحد مفعّلاً على الأقل");
        await EnsureUniqueOrderTypeAsync(entity, ct);
        audit.Add(AuditActions.Update, "OrderType", entity.Id, new { before, after = ToDto(entity) });
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<ServiceTypeDto>> ServicesAsync(bool includeInactive, int? orderTypeId, CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .Where(s => includeInactive || s.IsActive)
            // A service without an order type is usable with every type.
            .Where(s => orderTypeId == null || s.OrderTypeId == null || s.OrderTypeId == orderTypeId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new ServiceTypeDto(s.Id, s.Name, s.Description, s.DefaultPrice, s.OrderTypeId, s.OrderType != null ? s.OrderType.Name : null, s.IsActive, s.SortOrder))
            .ToListAsync(ct);

    public async Task<ServiceTypeDto> CreateServiceAsync(ServiceTypeRequest req, CancellationToken ct)
    {
        var entity = new ServiceType { Name = req.Name.Trim() };
        await ApplyAsync(entity, req, ct);
        db.Services.Add(entity);
        await db.SaveChangesAsync(ct);
        audit.Add(AuditActions.Create, "Service", entity.Id, new { entity.Name, entity.DefaultPrice, entity.OrderTypeId });
        await db.SaveChangesAsync(ct);
        return await GetServiceAsync(entity.Id, ct);
    }

    public async Task<ServiceTypeDto> UpdateServiceAsync(int id, ServiceTypeRequest req, CancellationToken ct)
    {
        var entity = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw AppException.NotFound("الخدمة غير موجودة");
        var before = new { entity.Name, entity.DefaultPrice, entity.OrderTypeId, entity.IsActive };
        await ApplyAsync(entity, req, ct);
        audit.Add(AuditActions.Update, "Service", entity.Id, new { before, after = new { entity.Name, entity.DefaultPrice, entity.OrderTypeId, entity.IsActive } });
        await db.SaveChangesAsync(ct);
        return await GetServiceAsync(id, ct);
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken ct) =>
        await db.PaymentMethods.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.Id)
            .Select(m => new PaymentMethodDto(m.Id, m.Code, m.Name, m.IsActive))
            .ToListAsync(ct);

    private async Task<ServiceTypeDto> GetServiceAsync(int id, CancellationToken ct) =>
        (await db.Services.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new ServiceTypeDto(s.Id, s.Name, s.Description, s.DefaultPrice, s.OrderTypeId, s.OrderType != null ? s.OrderType.Name : null, s.IsActive, s.SortOrder))
            .FirstOrDefaultAsync(ct))!;

    private static void Apply(OrderType entity, OrderTypeRequest req)
    {
        entity.Code = req.Code.Trim();
        entity.Name = req.Name.Trim();
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
    }

    private async Task EnsureUniqueOrderTypeAsync(OrderType entity, CancellationToken ct)
    {
        if (await db.OrderTypes.AnyAsync(t => t.Id != entity.Id && (t.Code == entity.Code || t.Name == entity.Name), ct))
            throw AppException.Conflict("يوجد نوع طلب بنفس الرمز أو الاسم");
    }

    private async Task ApplyAsync(ServiceType entity, ServiceTypeRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (name.Length == 0) throw AppException.BadRequest("اسم الخدمة مطلوب");
        if (await db.Services.AnyAsync(s => s.Id != entity.Id && s.Name == name, ct)) throw AppException.Conflict("توجد خدمة بنفس الاسم");
        if (req.OrderTypeId.HasValue && !await db.OrderTypes.AnyAsync(t => t.Id == req.OrderTypeId, ct)) throw AppException.BadRequest("نوع الطلب غير موجود");

        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        entity.DefaultPrice = req.DefaultPrice is { } p ? Math.Round(p, 2, MidpointRounding.AwayFromZero) : null;
        entity.OrderTypeId = req.OrderTypeId;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
    }

    private static OrderTypeDto ToDto(OrderType t) => new(t.Id, t.Code, t.Name, t.IsActive, t.SortOrder);
}
