using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class EmployeesController(EmployeeService employees, OrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<EmployeeListItemDto>> List([FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken ct) =>
        employees.ListAsync(search, isActive, ct);

    [HttpGet("lookup")]
    public Task<IReadOnlyList<EmployeeLookupDto>> Lookup(CancellationToken ct) => employees.LookupAsync(ct);

    [HttpGet("{id:int}")]
    public Task<EmployeeDetailDto> Get(int id, CancellationToken ct) => employees.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<EmployeeDetailDto>> Create(CreateEmployeeRequest req, CancellationToken ct)
    {
        var e = await employees.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = e.Id }, e);
    }

    [HttpPut("{id:int}")]
    public Task<EmployeeDetailDto> Update(int id, UpdateEmployeeRequest req, CancellationToken ct) => employees.UpdateAsync(id, req, ct);

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, SetStatusRequest req, CancellationToken ct)
    {
        await employees.SetStatusAsync(id, req.IsActive, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/login-link")]
    public Task<EmployeeDetailDto> RegenerateLoginLink(int id, CancellationToken ct) => employees.RegenerateLoginLinkAsync(id, ct);

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest req, CancellationToken ct)
    {
        await employees.ResetPasswordAsync(id, req.NewPassword, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/deduction")]
    public async Task<IActionResult> SetDeduction(int id, SetDeductionRequest req, CancellationToken ct)
    {
        await employees.SetDeductionAsync(id, req, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/deduction-history")]
    public Task<IReadOnlyList<DeductionRuleDto>> DeductionHistory(int id, CancellationToken ct) => employees.DeductionHistoryAsync(id, ct);

    [HttpGet("{id:int}/stats")]
    public Task<EmployeeStatsDto> Stats(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        employees.StatsAsync(id, from, to, ct);

    [HttpGet("{id:int}/orders")]
    public async Task<PagedResult<OrderListItemDto>> Orders(int id, [FromQuery] OrderQuery query, CancellationToken ct)
    {
        await employees.GetAsync(id, ct);
        return await orders.ListAsync(query with { EmployeeId = id }, ct);
    }
}
