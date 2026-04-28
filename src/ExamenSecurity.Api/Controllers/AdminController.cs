using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public sealed class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetUsersAsync(cancellationToken));
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserSummaryResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var response = await adminService.CreateUserAsync(request, cancellationToken);
        if (response is null)
        {
            return BadRequest(new { message = "Rol invalido para la demo." });
        }

        return CreatedAtAction(nameof(GetUsers), new { id = response.Id }, response);
    }

    [HttpPost("users/{userId:guid}/disable")]
    public async Task<IActionResult> DisableUser(Guid userId, DisableUserRequest request, CancellationToken cancellationToken)
    {
        var disabled = await adminService.DisableUserAsync(userId, request, User.GetUserId(), cancellationToken);
        if (!disabled)
        {
            return NotFound();
        }

        return Ok(new
        {
            message = "Usuario deshabilitado.",
            a09 = "Version main vulnerable: accion administrativa ejecutada sin auditoria de seguridad."
        });
    }

    [HttpGet("api-clients")]
    public async Task<ActionResult<IReadOnlyList<ApiClientResponse>>> GetApiClients(CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetApiClientsAsync(cancellationToken));
    }

    [HttpPost("api-clients/{clientId:guid}/disable")]
    public async Task<ActionResult<ApiClientResponse>> DisableApiClient(Guid clientId, CancellationToken cancellationToken)
    {
        var response = await adminService.DisableApiClientAsync(clientId, User.GetUserId(), cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPost("configuration")]
    public async Task<ActionResult<ConfigurationChangeResponse>> ChangeConfiguration(
        ConfigurationChangeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await adminService.ChangeConfigurationAsync(request, User.GetUserId(), cancellationToken);
        return Ok(response);
    }
}
