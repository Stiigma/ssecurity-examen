using ExamenSecurity.Api.DTOs;

namespace ExamenSecurity.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<PasswordResetResponse> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);
}
