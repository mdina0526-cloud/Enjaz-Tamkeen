using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

[ApiController]
[Route("api/daily-close")]
[Authorize(Roles = RoleNames.Finance)]
public class DailyCloseController(DailyCloseService service) : ControllerBase
{
    [HttpGet]
    public Task<List<DailyClose>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("preview")]
    public Task<DailyClosePreview> Preview([FromQuery] DateOnly date, CancellationToken ct) => service.PreviewAsync(date, ct);

    [HttpPost]
    public Task<DailyClose> Close(DailyCloseRequest request, CancellationToken ct) => service.CloseAsync(request, ct);
}
