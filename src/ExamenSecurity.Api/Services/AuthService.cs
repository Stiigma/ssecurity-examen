using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class AuthService(
    AppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ISecurityAuditService securityAuditService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.LoginFailed,
                    SecuritySeverity.Warning,
                    "Rejected",
                    "Intento de login con usuario inexistente o credenciales invalidas.",
                    Username: normalizedEmail,
                    StatusCode: StatusCodes.Status401Unauthorized),
                cancellationToken);

            return null;
        }

        if (!user.IsEnabled)
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.DisabledAccountLoginAttempt,
                    SecuritySeverity.High,
                    "Rejected",
                    "Intento de autenticacion contra una cuenta deshabilitada.",
                    UserId: user.Id,
                    Username: user.Email,
                    Role: user.Role,
                    StatusCode: StatusCodes.Status401Unauthorized),
                cancellationToken);

            return null;
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.LoginFailed,
                    SecuritySeverity.Warning,
                    "Rejected",
                    "Intento de login fallido por credenciales invalidas.",
                    UserId: user.Id,
                    Username: user.Email,
                    Role: user.Role,
                    StatusCode: StatusCodes.Status401Unauthorized),
                cancellationToken);

            return null;
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.CreateToken(user);
        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.LoginSucceeded,
                SecuritySeverity.Info,
                "Succeeded",
                "Login exitoso.",
                UserId: user.Id,
                Username: user.Email,
                Role: user.Role,
                StatusCode: StatusCodes.Status200OK),
            cancellationToken);

        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            new UserSummaryResponse(user.Id, user.Email, user.FullName, user.Role, user.Department, user.IsEnabled));
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);

        return user is null
            ? null
            : new CurrentUserResponse(user.Id, user.Email, user.FullName, user.Role, user.Department, user.IsEnabled);
    }

    public async Task<PasswordResetResponse> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail,
            cancellationToken);

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.PasswordResetRequested,
                SecuritySeverity.Medium,
                "Accepted",
                "Solicitud de recuperacion de password recibida.",
                UserId: user?.Id,
                Username: normalizedEmail,
                Role: user?.Role,
                StatusCode: StatusCodes.Status200OK),
            cancellationToken);

        var response = new PasswordResetResponse(
            "Si el correo existe, se enviaran instrucciones de recuperacion.",
            "Version fixed: la solicitud queda registrada como evento de seguridad sin exponer si el correo existe.");

        return response;
    }
}
