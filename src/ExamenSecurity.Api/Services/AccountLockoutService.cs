using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamenSecurity.Api.Services;

public interface IAccountLockoutService
{
    Task<AccountLockout?> LockAsync(
        LockoutTargetType targetType,
        string targetValue,
        TimeSpan duration,
        string reason,
        Guid? alertId,
        CancellationToken cancellationToken);

    Task<bool> IsLockedAsync(string targetValue, CancellationToken cancellationToken);

    Task<AccountLockout?> GetActiveLockoutAsync(string targetValue, CancellationToken cancellationToken);

    Task<bool> UnlockAsync(Guid lockoutId, Guid adminUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountLockout>> GetActiveLockoutsAsync(CancellationToken cancellationToken);
}

public sealed class AccountLockoutService(
    AppDbContext dbContext,
    IOptions<AccountLockoutOptions> options,
    ISecurityAuditService securityAuditService,
    ILogger<AccountLockoutService> logger) : IAccountLockoutService
{
    private readonly AccountLockoutOptions _options = options.Value;

    public async Task<AccountLockout?> LockAsync(
        LockoutTargetType targetType,
        string targetValue,
        TimeSpan duration,
        string reason,
        Guid? alertId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Account lockout is disabled. Skipping lock for {TargetValue}.", targetValue);
            return null;
        }

        var normalizedValue = targetValue.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        var existing = await dbContext.AccountLockouts
            .FirstOrDefaultAsync(l =>
                l.TargetValue == normalizedValue &&
                l.TargetType == targetType &&
                l.IsActive &&
                l.LockedUntilUtc > now,
            cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation(
                "Active lockout already exists for {TargetValue}. Extending duration.",
                normalizedValue);

            existing.LockedUntilUtc = now.Add(duration);
            existing.Reason = $"{existing.Reason} | Extended: {reason}";
            await dbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var lockout = new AccountLockout
        {
            Id = Guid.NewGuid(),
            TargetType = targetType,
            TargetValue = normalizedValue,
            Reason = reason,
            LockedUntilUtc = now.Add(duration),
            CreatedAtUtc = now,
            IsActive = true,
            AlertId = alertId
        };

        dbContext.AccountLockouts.Add(lockout);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Account lockout created. Target={TargetValue}, Type={TargetType}, Until={LockedUntilUtc}, Reason={Reason}",
            normalizedValue,
            targetType,
            lockout.LockedUntilUtc,
            reason);

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.AccountLocked,
                SecuritySeverity.Warning,
                "Locked",
                $"Bloqueo temporal aplicado a {targetType} '{normalizedValue}'. Razón: {reason}",
                StatusCode: StatusCodes.Status423Locked,
                Metadata: new Dictionary<string, object?>
                {
                    ["targetType"] = targetType.ToString(),
                    ["targetValue"] = normalizedValue,
                    ["lockedUntilUtc"] = lockout.LockedUntilUtc,
                    ["reason"] = reason,
                    ["alertId"] = alertId
                }),
            cancellationToken);

        return lockout;
    }

    public async Task<bool> IsLockedAsync(string targetValue, CancellationToken cancellationToken)
    {
        var active = await GetActiveLockoutAsync(targetValue, cancellationToken);
        return active is not null;
    }

    public async Task<AccountLockout?> GetActiveLockoutAsync(string targetValue, CancellationToken cancellationToken)
    {
        var normalizedValue = targetValue.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        return await dbContext.AccountLockouts
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.TargetValue == normalizedValue &&
                l.IsActive &&
                l.LockedUntilUtc > now,
            cancellationToken);
    }

    public async Task<bool> UnlockAsync(Guid lockoutId, Guid adminUserId, CancellationToken cancellationToken)
    {
        var lockout = await dbContext.AccountLockouts
            .FirstOrDefaultAsync(l => l.Id == lockoutId, cancellationToken);

        if (lockout is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        lockout.IsActive = false;
        lockout.UnlockedAtUtc = now;
        lockout.UnlockedByUserId = adminUserId;
        lockout.LockedUntilUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Account lockout manually unlocked. LockoutId={LockoutId}, AdminUserId={AdminUserId}",
            lockoutId,
            adminUserId);

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.AccountUnlocked,
                SecuritySeverity.Info,
                "Unlocked",
                $"Bloqueo '{lockout.TargetValue}' desbloqueado manualmente por administrador.",
                UserId: adminUserId,
                StatusCode: StatusCodes.Status200OK,
                Metadata: new Dictionary<string, object?>
                {
                    ["lockoutId"] = lockoutId,
                    ["targetType"] = lockout.TargetType.ToString(),
                    ["targetValue"] = lockout.TargetValue,
                    ["unlockedByUserId"] = adminUserId
                }),
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<AccountLockout>> GetActiveLockoutsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        return await dbContext.AccountLockouts
            .AsNoTracking()
            .Where(l => l.IsActive && l.LockedUntilUtc > now)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
