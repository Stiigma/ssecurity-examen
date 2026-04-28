using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class SecurityAlertService(AppDbContext dbContext) : ISecurityAlertService
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
            await CreateAlertIfMissingAsync(
                SecurityAlertType.MultipleFailedLogins,
                SecuritySeverity.High,
                "Multiples intentos fallidos de login",
                $"Se detectaron {count} intentos fallidos de autenticacion en una ventana de 10 minutos.",
                securityEvent,
                count,
                cancellationToken);
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
            await CreateAlertIfMissingAsync(
                SecurityAlertType.DisabledAccountTargeted,
                SecuritySeverity.High,
                "Cuenta deshabilitada bajo intento de acceso",
                $"Se detectaron {count} intentos contra una cuenta deshabilitada.",
                securityEvent,
                count,
                cancellationToken);
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

    private async Task CreateAlertIfMissingAsync(
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
            return;
        }

        dbContext.SecurityAlerts.Add(new SecurityAlert
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
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
