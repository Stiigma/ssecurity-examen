using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class AdminService(AppDbContext dbContext, ISecurityAuditService securityAuditService) : IAdminService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => new UserSummaryResponse(user.Id, user.Email, user.FullName, user.Role, user.Department, user.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserSummaryResponse?> CreateUserAsync(CreateUserRequest request, Guid changedByUserId, CancellationToken cancellationToken)
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

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.AdminUserCreated,
                role == Roles.Admin ? SecuritySeverity.High : SecuritySeverity.Medium,
                "Succeeded",
                "Administrador creo un usuario.",
                UserId: changedByUserId,
                StatusCode: StatusCodes.Status201Created,
                ResourceType: "User",
                ResourceId: user.Id.ToString(),
                Metadata: new Dictionary<string, object?>
                {
                    ["createdUserEmail"] = user.Email,
                    ["createdUserRole"] = user.Role
                }),
            cancellationToken);

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

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.AdminUserDisabled,
                SecuritySeverity.High,
                "Succeeded",
                "Administrador deshabilito un usuario.",
                UserId: changedByUserId,
                StatusCode: StatusCodes.Status200OK,
                ResourceType: "User",
                ResourceId: user.Id.ToString(),
                Metadata: new Dictionary<string, object?>
                {
                    ["disabledUserEmail"] = user.Email,
                    ["reason"] = request.Reason
                }),
            cancellationToken);

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

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.ApiClientDisabled,
                SecuritySeverity.High,
                "Succeeded",
                "Administrador deshabilito un cliente de API.",
                UserId: changedByUserId,
                StatusCode: StatusCodes.Status200OK,
                ResourceType: "ApiClient",
                ResourceId: client.Id.ToString(),
                Metadata: new Dictionary<string, object?>
                {
                    ["clientName"] = client.Name,
                    ["ownerTeam"] = client.OwnerTeam
                }),
            cancellationToken);

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

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.ConfigurationChanged,
                SecuritySeverity.High,
                "Succeeded",
                "Administrador modifico una configuracion sensible.",
                UserId: changedByUserId,
                StatusCode: StatusCodes.Status200OK,
                ResourceType: "Configuration",
                ResourceId: change.SettingKey,
                Metadata: new Dictionary<string, object?>
                {
                    ["settingKey"] = change.SettingKey,
                    ["previousValue"] = change.PreviousValue,
                    ["newValue"] = change.NewValue
                }),
            cancellationToken);

        return new ConfigurationChangeResponse(
            change.Id,
            change.SettingKey,
            change.PreviousValue,
            change.NewValue,
            change.ChangedByUserId,
            change.ChangedAtUtc);
    }
}
