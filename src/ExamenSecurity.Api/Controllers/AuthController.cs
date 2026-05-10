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
        var result = await authService.LoginAsync(request, cancellationToken);
        if (result.IsLocked)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = result.ErrorMessage,
                a09 = "Version fixed: la cuenta o IP esta bloqueada temporalmente."
            });
        }

        if (!result.IsSuccess)
        {
            return Unauthorized(new
            {
                message = result.ErrorMessage,
                a09 = "Version fixed: el intento fallido quedo registrado como evento de seguridad."
            });
        }

        return Ok(result.LoginResponse);
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
