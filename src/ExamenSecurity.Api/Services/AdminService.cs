using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class AdminService(AppDbContext dbContext) : IAdminService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => new UserSummaryResponse(user.Id, user.Email, user.FullName, user.Role, user.Department, user.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserSummaryResponse?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var role = request.Role.Trim();
        if (role is not (Roles.Admin or Roles.Student or Roles.Auditor))
        {
            return null;
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            Role = role,
            Department = request.Department.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Vulnerable A09 demo:
        // Creating a privileged or normal user is a sensitive administrative action,
        // but this main branch has no audit trail that captures actor, target, reason, or timestamp.
        return new UserSummaryResponse(user.Id, user.Email, user.FullName, user.Role, user.Department, user.IsEnabled);
    }

    public async Task<bool> DisableUserAsync(Guid userId, DisableUserRequest request, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.IsEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Vulnerable A09 demo:
        // The reason and actor are received, but are not persisted in a security audit log.
        // During an incident, the team cannot prove who disabled the account or why.
        _ = request;
        _ = changedByUserId;

        return true;
    }

    public async Task<IReadOnlyList<ApiClientResponse>> GetApiClientsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.ApiClients
            .AsNoTracking()
            .OrderBy(client => client.Name)
            .Select(client => new ApiClientResponse(client.Id, client.Name, client.OwnerTeam, client.IsEnabled, client.CreatedAtUtc, client.DisabledAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiClientResponse?> DisableApiClientAsync(Guid clientId, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var client = await dbContext.ApiClients.FirstOrDefaultAsync(candidate => candidate.Id == clientId, cancellationToken);
        if (client is null)
        {
            return null;
        }

        client.IsEnabled = false;
        client.DisabledAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Vulnerable A09 demo:
        // Disabling an integration is operationally sensitive, but no alert or admin audit entry is generated.
        _ = changedByUserId;

        return new ApiClientResponse(client.Id, client.Name, client.OwnerTeam, client.IsEnabled, client.CreatedAtUtc, client.DisabledAtUtc);
    }

    public async Task<ConfigurationChangeResponse> ChangeConfigurationAsync(
        ConfigurationChangeRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var change = new ConfigurationChange
        {
            Id = Guid.NewGuid(),
            ChangedByUserId = changedByUserId,
            SettingKey = request.SettingKey.Trim(),
            PreviousValue = "demo-current-value",
            NewValue = request.NewValue.Trim(),
            ChangedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.ConfigurationChanges.Add(change);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Vulnerable A09 demo:
        // This stores the business change, but it is not treated as a security event,
        // does not include request metadata, and does not alert on risky settings.
        return new ConfigurationChangeResponse(
            change.Id,
            change.SettingKey,
            change.PreviousValue,
            change.NewValue,
            change.ChangedByUserId,
            change.ChangedAtUtc);
    }
}
