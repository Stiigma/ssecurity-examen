using ExamenSecurity.Api.DTOs;

namespace ExamenSecurity.Api.Services;

public interface IAdminService
{
    Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserSummaryResponse?> CreateUserAsync(CreateUserRequest request, Guid changedByUserId, CancellationToken cancellationToken);
    Task<bool> DisableUserAsync(Guid userId, DisableUserRequest request, Guid changedByUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiClientResponse>> GetApiClientsAsync(CancellationToken cancellationToken);
    Task<ApiClientResponse?> DisableApiClientAsync(Guid clientId, Guid changedByUserId, CancellationToken cancellationToken);
    Task<ConfigurationChangeResponse> ChangeConfigurationAsync(ConfigurationChangeRequest request, Guid changedByUserId, CancellationToken cancellationToken);
}
