using Microsoft.AspNetCore.Mvc;
using Summary.Api.Application.Services;
using Summary.Api.Domain.Events;

namespace Summary.Api.Controllers;

[ApiController]
[Route("internal/events")]
public sealed class ProjectionController(ProjectionService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Apply(ProjectionEvent message, CancellationToken token)
    {
        var applied = await service.ApplyAsync(message, token);
        return applied ? Accepted() : Ok(new { duplicate = true });
    }
}
