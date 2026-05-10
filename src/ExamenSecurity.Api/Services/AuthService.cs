using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class AuthService(
    AppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ISecurityAuditService securityAuditService,
    IAccountLockoutService lockoutService,
    IImpossibleTravelService impossibleTravelService,
    IHttpContextAccessor httpContextAccessor) : IAuthService
{
    public async Task<AuthenticationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var clientIp = GetClientIp();

        // Check if the IP is locked
        if (!string.IsNullOrEmpty(clientIp) && await lockoutService.IsLockedAsync(clientIp, cancellationToken))
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.LockoutAttemptDuringLock,
                    SecuritySeverity.Warning,
                    "Rejected",
                    "Intento de login desde una IP bloqueada temporalmente.",
                    Username: normalizedEmail,
                    StatusCode: StatusCodes.Status423Locked,
                    Metadata: new Dictionary<string, object?>
                    {
                        ["clientIp"] = clientIp
                    }),
                cancellationToken);

            return new AuthenticationResult(false, true, null, "Cuenta bloqueada temporalmente por seguridad.");
        }

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

            return new AuthenticationResult(false, false, null, "Credenciales invalidas.");
        }

        // Check if the user account is locked
        if (await lockoutService.IsLockedAsync(user.Email, cancellationToken))
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.LockoutAttemptDuringLock,
                    SecuritySeverity.Warning,
                    "Rejected",
                    "Intento de login contra una cuenta bloqueada temporalmente.",
                    UserId: user.Id,
                    Username: user.Email,
                    Role: user.Role,
                    StatusCode: StatusCodes.Status423Locked),
                cancellationToken);

            return new AuthenticationResult(false, true, null, "Cuenta bloqueada temporalmente por seguridad.");
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

            return new AuthenticationResult(false, false, null, "Credenciales invalidas.");
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

            return new AuthenticationResult(false, false, null, "Credenciales invalidas.");
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

        await impossibleTravelService.EvaluateAsync(user, clientIp ?? string.Empty, cancellationToken);

        return new AuthenticationResult(
            true,
            false,
            new LoginResponse(
                token.Token,
                token.ExpiresAtUtc,
                new UserSummaryResponse(user.Id, user.Email, user.FullName, user.Role, user.Department, user.IsEnabled)),
            null);
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

    private string? GetClientIp()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
