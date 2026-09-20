using Core.Api.Application.Interfaces;
using Core.Api.Application.Models;
using Core.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Core.Api.Controllers;

[ApiController]
[Route("identity/users")]
public sealed class IdentityController(
    ICurrentKeycloakAuthorization authorization,
    IKeycloakAdminClient admin) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListUsers(CancellationToken token)
    {
        if (!await IsAdminAsync(token))
            return Forbid();

        return Ok(await admin.ListUsersAsync(token));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateManagedUserRequest request, CancellationToken token)
    {
        if (!await IsAdminAsync(token))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Username) ||
            !SupportedRoles.Contains(request.Role, StringComparer.Ordinal))
        {
            return ValidationProblem(new Dictionary<string, string[]>
            {
                ["user"] = ["username and a supported role are required"]
            });
        }

        var user = await admin.CreateUserAsync(request, token);

        return user is null
            ? Conflict(new { code = "user_exists" })
            : Created($"/identity/users/{user.Id}", user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
        string id,
        UpdateManagedUserRequest request,
        CancellationToken token)
    {
        if (!await IsAdminAsync(token))
            return Forbid();

        if (!SupportedRoles.Contains(request.Role, StringComparer.Ordinal))
        {
            return ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["unsupported role"]
            });
        }

        try
        {
            return await admin.UpdateUserAsync(id, request, token)
                ? NoContent()
                : NotFound();
        }
        catch (LastActiveAdminException)
        {
            return Conflict(new { code = "last_active_admin" });
        }
    }

    private async Task<bool> IsAdminAsync(CancellationToken token)
    {
        if (User.Identity?.IsAuthenticated != true)
            return false;

        var state = await authorization.ConfirmAsync(User, token);
        return state.Enabled && state.Roles.Contains("admin");
    }

    private static readonly string[] SupportedRoles = ["admin", "operator", "auditor"];
}
