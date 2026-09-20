using Microsoft.AspNetCore.Mvc;
using Summary.Api.Domain.Events;
using Summary.Api.Infrastructure.Persistence;

namespace Summary.Api.Controllers;

[ApiController]
[Route("internal/events")]
public sealed class ProjectionController(ProjectionService projection) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Apply(
        ProjectionEvent message,
        CancellationToken token)
    {
        var applied = await projection.ApplyAsync(message, token);

        return applied
            ? Accepted()
            : Ok(new { duplicate = true });
    }
}
