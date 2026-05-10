using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamenSecurity.Api.Services;

public sealed class SecurityAlertService(
    AppDbContext dbContext,
    IServiceProvider serviceProvider,
    IOptions<AccountLockoutOptions> lockoutOptions,
    ILogger<SecurityAlertService> logger) : ISecurityAlertService
{
    private static readonly TimeSpan DetectionWindow = TimeSpan.FromMinutes(10);

    public async Task EvaluateAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        switch (securityEvent.EventType)
        {
            case SecurityEventType.LoginFailed:
                await EvaluateMultipleFailedLoginsAsync(securityEvent, cancellationToken);
                break;
            case SecurityEventType.DisabledAccountLoginAttempt:
                await EvaluateDisabledAccountTargetedAsync(securityEvent, cancellationToken);
                break;
            case SecurityEventType.AccessDenied:
                await EvaluateAdminEndpointProbingAsync(securityEvent, cancellationToken);
                break;
            case SecurityEventType.StudentRecordAccessDenied:
                await EvaluateStudentRecordProbingAsync(securityEvent, cancellationToken);
                break;
            case SecurityEventType.AdminUserDisabled:
            case SecurityEventType.ApiClientDisabled:
            case SecurityEventType.ConfigurationChanged:
                await CreateAlertIfMissingAsync(
                    SecurityAlertType.SensitiveAdminChange,
                    SecuritySeverity.High,
                    "Cambio administrativo sensible",
                    securityEvent.Message,
                    securityEvent,
                    1,
                    cancellationToken);
                break;
            case SecurityEventType.SecurityTicketCreated:
                await CreateAlertIfMissingAsync(
                    SecurityAlertType.SecurityTicketRequiresReview,
                    SecuritySeverity.Medium,
                    "Ticket de seguridad requiere revision",
                    securityEvent.Message,
                    securityEvent,
                    1,
                    cancellationToken);
                break;
            case SecurityEventType.UnhandledException:
                await EvaluateRepeatedUnhandledErrorsAsync(securityEvent, cancellationToken);
                break;
            case SecurityEventType.HoneytokenTriggered:
                await CreateHoneytokenAlertAsync(securityEvent, cancellationToken);
                break;
            case SecurityEventType.ImpossibleTravelDetected:
                await CreateAlertIfMissingAsync(
                    SecurityAlertType.ImpossibleTravel,
                    SecuritySeverity.High,
                    "Viaje imposible detectado",
                    securityEvent.Message,
                    securityEvent,
                    1,
                    cancellationToken);
                break;
        }
    }

    public async Task<bool> AcknowledgeAsync(Guid alertId, Guid acknowledgedByUserId, CancellationToken cancellationToken)
    {
        var alert = await dbContext.SecurityAlerts.FirstOrDefaultAsync(candidate => candidate.Id == alertId, cancellationToken);
        if (alert is null)
        {
            return false;
        }

        alert.IsAcknowledged = true;
        alert.AcknowledgedByUserId = acknowledgedByUserId;
        alert.AcknowledgedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EvaluateMultipleFailedLoginsAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.Subtract(DetectionWindow);
        var username = securityEvent.Username;
        var ipAddress = securityEvent.IpAddress;

        var count = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.EventType == SecurityEventType.LoginFailed &&
                candidate.CreatedAtUtc >= since &&
                ((!string.IsNullOrEmpty(username) && candidate.Username == username) ||
                 (!string.IsNullOrEmpty(ipAddress) && candidate.IpAddress == ipAddress)),
                cancellationToken);

        if (count >= 5)
        {
            var alert = await CreateAlertIfMissingAsync(
                SecurityAlertType.MultipleFailedLogins,
                SecuritySeverity.High,
                "Multiples intentos fallidos de login",
                $"Se detectaron {count} intentos fallidos de autenticacion en una ventana de 10 minutos.",
                securityEvent,
                count,
                cancellationToken);

            if (alert is not null)
            {
                await TriggerLockoutsAsync(securityEvent, alert, cancellationToken);
            }
        }
    }

    private async Task EvaluateDisabledAccountTargetedAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.Subtract(DetectionWindow);
        var username = securityEvent.Username;
        var count = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.EventType == SecurityEventType.DisabledAccountLoginAttempt &&
                candidate.CreatedAtUtc >= since &&
                candidate.Username == username,
                cancellationToken);

        if (count >= 2)
        {
            var alert = await CreateAlertIfMissingAsync(
                SecurityAlertType.DisabledAccountTargeted,
                SecuritySeverity.High,
                "Cuenta deshabilitada bajo intento de acceso",
                $"Se detectaron {count} intentos contra una cuenta deshabilitada.",
                securityEvent,
                count,
                cancellationToken);

            if (alert is not null)
            {
                await TriggerLockoutsAsync(securityEvent, alert, cancellationToken);
            }
        }
    }

    private async Task EvaluateAdminEndpointProbingAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        if (securityEvent.Path is null || !securityEvent.Path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var since = DateTimeOffset.UtcNow.Subtract(DetectionWindow);
        var userId = securityEvent.UserId;
        var ipAddress = securityEvent.IpAddress;
        var count = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.EventType == SecurityEventType.AccessDenied &&
                candidate.CreatedAtUtc >= since &&
                candidate.Path != null &&
                candidate.Path.StartsWith("/api/admin") &&
                ((userId != null && candidate.UserId == userId) ||
                 (!string.IsNullOrEmpty(ipAddress) && candidate.IpAddress == ipAddress)),
                cancellationToken);

        if (count >= 3)
        {
            await CreateAlertIfMissingAsync(
                SecurityAlertType.AdminEndpointProbing,
                SecuritySeverity.High,
                "Sondeo de endpoints administrativos",
                $"Se detectaron {count} accesos denegados a endpoints administrativos.",
                securityEvent,
                count,
                cancellationToken);
        }
    }

    private async Task EvaluateStudentRecordProbingAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.Subtract(DetectionWindow);
        var userId = securityEvent.UserId;
        var count = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.EventType == SecurityEventType.StudentRecordAccessDenied &&
                candidate.CreatedAtUtc >= since &&
                userId != null &&
                candidate.UserId == userId,
                cancellationToken);

        if (count >= 2)
        {
            await CreateAlertIfMissingAsync(
                SecurityAlertType.StudentRecordProbing,
                SecuritySeverity.High,
                "Sondeo de expedientes academicos",
                $"Se detectaron {count} intentos de consultar expedientes ajenos.",
                securityEvent,
                count,
                cancellationToken);
        }
    }

    private async Task EvaluateRepeatedUnhandledErrorsAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.Subtract(DetectionWindow);
        var path = securityEvent.Path;
        var count = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.EventType == SecurityEventType.UnhandledException &&
                candidate.CreatedAtUtc >= since &&
                candidate.Path == path,
                cancellationToken);

        if (count >= 3)
        {
            await CreateAlertIfMissingAsync(
                SecurityAlertType.RepeatedUnhandledErrors,
                SecuritySeverity.Error,
                "Errores no controlados repetidos",
                $"Se detectaron {count} errores no controlados en el mismo endpoint.",
                securityEvent,
                count,
                cancellationToken);
        }
    }

    private async Task CreateHoneytokenAlertAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        // Honeytoken alerts are ALWAYS created immediately, without deduplication or time windows.
        var alert = new SecurityAlert
        {
            Id = Guid.NewGuid(),
            AlertType = SecurityAlertType.HoneytokenAccessed,
            Severity = SecuritySeverity.Critical,
            Title = "Endpoint senoelo (honeytoken) accedido",
            Description = $"Sondeo detectado en {securityEvent.Path} desde {securityEvent.IpAddress}.",
            RelatedUserId = securityEvent.UserId,
            RelatedUsername = securityEvent.Username,
            RelatedIpAddress = securityEvent.IpAddress,
            EventCount = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.SecurityAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SecurityAlert?> CreateAlertIfMissingAsync(
        SecurityAlertType alertType,
        SecuritySeverity severity,
        string title,
        string description,
        SecurityEvent securityEvent,
        int eventCount,
        CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.Subtract(DetectionWindow);
        var alreadyExists = await dbContext.SecurityAlerts.AnyAsync(candidate =>
            candidate.AlertType == alertType &&
            !candidate.IsAcknowledged &&
            candidate.CreatedAtUtc >= since &&
            candidate.RelatedUsername == securityEvent.Username &&
            candidate.RelatedIpAddress == securityEvent.IpAddress,
            cancellationToken);

        if (alreadyExists)
        {
            return null;
        }

        var alert = new SecurityAlert
        {
            Id = Guid.NewGuid(),
            AlertType = alertType,
            Severity = severity,
            Title = title,
            Description = description,
            RelatedUserId = securityEvent.UserId,
            RelatedUsername = securityEvent.Username,
            RelatedIpAddress = securityEvent.IpAddress,
            EventCount = eventCount,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.SecurityAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return alert;
    }

    private async Task TriggerLockoutsAsync(SecurityEvent securityEvent, SecurityAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            var options = lockoutOptions.Value;
            if (!options.Enabled)
            {
                return;
            }

            var duration = await CalculateLockoutDurationAsync(securityEvent, cancellationToken);
            var lockoutService = serviceProvider.GetRequiredService<IAccountLockoutService>();

            if (options.LockUserAccount && !string.IsNullOrWhiteSpace(securityEvent.Username))
            {
                await lockoutService.LockAsync(
                    LockoutTargetType.User,
                    securityEvent.Username,
                    duration,
                    $"Alerta {alert.AlertType}: {alert.Description}",
                    alert.Id,
                    cancellationToken);
            }

            if (options.LockIpAddress && !string.IsNullOrWhiteSpace(securityEvent.IpAddress))
            {
                await lockoutService.LockAsync(
                    LockoutTargetType.IpAddress,
                    securityEvent.IpAddress,
                    duration,
                    $"Alerta {alert.AlertType}: {alert.Description}",
                    alert.Id,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger lockout for alert {AlertId}.", alert.Id);
        }
    }

    private async Task<TimeSpan> CalculateLockoutDurationAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        var baseDuration = TimeSpan.FromMinutes(lockoutOptions.Value.BaseDurationMinutes);
        var username = securityEvent.Username;
        var ipAddress = securityEvent.IpAddress;

        var previousLockouts = await dbContext.AccountLockouts
            .AsNoTracking()
            .CountAsync(l =>
                l.CreatedAtUtc >= DateTimeOffset.UtcNow.AddDays(-1) &&
                ((l.TargetType == LockoutTargetType.User && l.TargetValue == username) ||
                 (l.TargetType == LockoutTargetType.IpAddress && l.TargetValue == ipAddress)),
            cancellationToken);

        // Exponential backoff: base * 2^previousLockouts (max 8 hours)
        var multiplier = Math.Pow(2, previousLockouts);
        var duration = TimeSpan.FromMinutes(baseDuration.TotalMinutes * multiplier);
        var maxDuration = TimeSpan.FromHours(8);

        return duration > maxDuration ? maxDuration : duration;
    }
}
