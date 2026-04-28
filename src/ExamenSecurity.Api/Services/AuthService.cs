using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class AuthService(AppDbContext dbContext, IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            // Vulnerable A09 demo:
            // Unknown account login attempts are rejected but not audited.
            // A defender cannot see enumeration attempts or password spraying attempts.
            return null;
        }

        if (!user.IsEnabled)
        {
            // Vulnerable A09 demo:
            // Attempts against disabled accounts are security-relevant, but this version leaves no durable trace.
            return null;
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Vulnerable A09 demo:
            // Failed logins are not counted, correlated by IP/user, persisted, or alerted.
            return null;
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.CreateToken(user);

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

    public Task<PasswordResetResponse> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        // Vulnerable A09 demo:
        // The response is intentionally generic, but the event is not audited. Repeated reset abuse
        // cannot be detected later because there is no security event store or alert threshold.
        var response = new PasswordResetResponse(
            "Si el correo existe, se enviaran instrucciones de recuperacion.",
            "Version main vulnerable: la solicitud no queda auditada ni genera alerta por abuso repetido.");

        return Task.FromResult(response);
    }
}
