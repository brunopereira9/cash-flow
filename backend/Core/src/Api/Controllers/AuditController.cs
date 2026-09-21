using Core.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Core.Api.Controllers;

[ApiController]
[Route("audit")]
public sealed class AuditController(AuditService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? actor,
        [FromQuery] string? operation,
        [FromQuery] string? correlation,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken token) =>
        Ok(await service.ListAsync(actor, operation, correlation, from, to, token));
}
