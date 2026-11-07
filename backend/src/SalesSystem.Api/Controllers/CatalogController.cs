using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

/// <summary>Order types, service catalog and payment methods. Reading is open to signed-in users; changes are admin-only.</summary>
[ApiController]
[Route("api/catalog")]
[Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Employee}")]
public class CatalogController(CatalogService catalog, ICurrentUser currentUser) : ControllerBase
{
    /// <summary><c>includeInactive</c> is honoured for admins only.</summary>
    [HttpGet("order-types")]
    public Task<IReadOnlyList<OrderTypeDto>> OrderTypes([FromQuery] bool includeInactive, CancellationToken ct) =>
        catalog.OrderTypesAsync(includeInactive && currentUser.IsAdmin, ct);

    [HttpPost("order-types")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<OrderTypeDto>> CreateOrderType(OrderTypeRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateOrderTypeAsync(req, ct));

    [HttpPut("order-types/{id:int}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public Task<OrderTypeDto> UpdateOrderType(int id, OrderTypeRequest req, CancellationToken ct) => catalog.UpdateOrderTypeAsync(id, req, ct);

    [HttpGet("services")]
    public Task<IReadOnlyList<ServiceTypeDto>> Services([FromQuery] bool includeInactive, [FromQuery] int? orderTypeId, CancellationToken ct) =>
        catalog.ServicesAsync(includeInactive && currentUser.IsAdmin, orderTypeId, ct);

    [HttpPost("services")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<ServiceTypeDto>> CreateService(ServiceTypeRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateServiceAsync(req, ct));

    [HttpPut("services/{id:int}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public Task<ServiceTypeDto> UpdateService(int id, ServiceTypeRequest req, CancellationToken ct) => catalog.UpdateServiceAsync(id, req, ct);

    [HttpGet("payment-methods")]
    public Task<IReadOnlyList<PaymentMethodDto>> PaymentMethods(CancellationToken ct) => catalog.PaymentMethodsAsync(ct);
}
