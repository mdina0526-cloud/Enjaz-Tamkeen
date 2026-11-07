using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Employee}")]
public class OrdersController(OrderService orders) : ControllerBase
{
    /// <summary>Admins see all orders; employees only see their own (enforced server-side).</summary>
    [HttpGet]
    public Task<PagedResult<OrderListItemDto>> List([FromQuery] OrderQuery query, CancellationToken ct) => orders.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<OrderDetailDto> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);

    [HttpGet("deduction-info")]
    public Task<DeductionInfoDto> DeductionInfo([FromQuery] int? employeeId, CancellationToken ct) => orders.DeductionInfoAsync(employeeId, ct);

    /// <summary>Send an <c>Idempotency-Key</c> header so retries of the same submission never create a second order or payment.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> Create(
        CreateOrderRequest req, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var (order, replayed) = await orders.CreateAsync(req, idempotencyKey, ct);
        if (replayed) return Ok(order);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public Task<OrderDetailDto> Approve(int id, CancellationToken ct) => orders.ApproveAsync(id, ct);

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public Task<OrderDetailDto> Cancel(int id, CancelOrderRequest req, CancellationToken ct) => orders.CancelAsync(id, req.Reason, ct);
}
