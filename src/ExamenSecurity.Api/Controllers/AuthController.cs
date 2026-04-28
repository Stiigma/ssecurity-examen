using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Security;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        if (response is null)
        {
            // Vulnerable A09 demo:
            // A failed authentication event is security-relevant, but this endpoint only returns 401.
            return Unauthorized(new
            {
                message = "Credenciales invalidas.",
                a09 = "Version main vulnerable: no se registro evento de login fallido ni se genero alerta."
            });
        }

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var response = await authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("password-reset/request")]
    public async Task<ActionResult<PasswordResetResponse>> RequestPasswordReset(
        PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RequestPasswordResetAsync(request, cancellationToken);
        return Ok(response);
    }
}
