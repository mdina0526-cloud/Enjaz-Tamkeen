using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

[ApiController]
[Route("api/rentals")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class RentalsController(RentalService rentals) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RentalListItemDto>> List([FromQuery] RentalQuery query, CancellationToken ct) => rentals.ListAsync(query, ct);

    [HttpGet("summary")]
    public Task<RentalSummaryDto> Summary([FromQuery] RentalQuery query, CancellationToken ct) => rentals.SummaryAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<RentalDetailDto> Get(int id, CancellationToken ct) => rentals.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<RentalDetailDto>> Create(RentalRequest req, CancellationToken ct)
    {
        var r = await rentals.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPut("{id:int}")]
    public Task<RentalDetailDto> Update(int id, RentalRequest req, CancellationToken ct) => rentals.UpdateAsync(id, req, ct);

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, [FromQuery] bool archived = true, CancellationToken ct = default)
    {
        await rentals.SetArchivedAsync(id, archived, ct);
        return NoContent();
    }

    /// <summary>Multipart form (optional receipt in <c>attachment</c>). Send an <c>Idempotency-Key</c> header to make retries safe.</summary>
    [HttpPost("{id:int}/payments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<RentalDetailDto>> AddPayment(
        int id, [FromForm] RentalPaymentRequest req, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var (rental, replayed) = await rentals.AddPaymentAsync(id, req, idempotencyKey, ct);
        return replayed ? Ok(rental) : StatusCode(StatusCodes.Status201Created, rental);
    }

    [HttpPost("{id:int}/payments/{paymentId:int}/void")]
    public Task<RentalDetailDto> VoidPayment(int id, int paymentId, VoidRequest req, CancellationToken ct) =>
        rentals.VoidPaymentAsync(id, paymentId, req.Reason, ct);

    [HttpGet("{id:int}/payments/{paymentId:int}/attachment")]
    public async Task<IActionResult> Attachment(int id, int paymentId, CancellationToken ct)
    {
        var (stream, contentType, name) = await rentals.GetAttachmentAsync(id, paymentId, ct);
        return File(stream, contentType, name);
    }
}
