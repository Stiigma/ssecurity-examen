using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Security;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.Admin},{Roles.Auditor}")]
[Route("api/security")]
public sealed class SecurityController(
    AppDbContext dbContext,
    ISecurityAlertService alertService,
    ISecurityAuditService securityAuditService) : ControllerBase
{
    [HttpGet("events")]
    public async Task<ActionResult<PagedResponse<SecurityEventResponse>>> GetEvents(
        [FromQuery] SecurityEventType? eventType,
        [FromQuery] SecuritySeverity? severity,
        [FromQuery] string? username,
        [FromQuery] string? ipAddress,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.SecurityEvents.AsNoTracking();

        if (eventType is not null)
        {
            query = query.Where(item => item.EventType == eventType);
        }

        if (severity is not null)
        {
            query = query.Where(item => item.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(item => item.Username != null && item.Username.Contains(username));
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            query = query.Where(item => item.IpAddress == ipAddress);
        }

        if (fromUtc is not null)
        {
            query = query.Where(item => item.CreatedAtUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(item => item.CreatedAtUtc <= toUtc);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SecurityEventResponse(
                item.Id,
                item.EventType,
                item.Severity,
                item.UserId,
                item.Username,
                item.Role,
                item.IpAddress,
                item.UserAgent,
                item.HttpMethod,
                item.Path,
                item.StatusCode,
                item.ResourceType,
                item.ResourceId,
                item.Outcome,
                item.Message,
                item.MetadataJson,
                item.CorrelationId,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<SecurityEventResponse>(items, page, pageSize, total));
    }

    [HttpGet("events/{id:guid}")]
    public async Task<ActionResult<SecurityEventResponse>> GetEvent(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.SecurityEvents
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new SecurityEventResponse(
                candidate.Id,
                candidate.EventType,
                candidate.Severity,
                candidate.UserId,
                candidate.Username,
                candidate.Role,
                candidate.IpAddress,
                candidate.UserAgent,
                candidate.HttpMethod,
                candidate.Path,
                candidate.StatusCode,
                candidate.ResourceType,
                candidate.ResourceId,
                candidate.Outcome,
                candidate.Message,
                candidate.MetadataJson,
                candidate.CorrelationId,
                candidate.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<PagedResponse<SecurityAlertResponse>>> GetAlerts(
        [FromQuery] SecurityAlertType? alertType,
        [FromQuery] SecuritySeverity? severity,
        [FromQuery] bool? onlyUnacknowledged,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.SecurityAlerts.AsNoTracking();

        if (alertType is not null)
        {
            query = query.Where(item => item.AlertType == alertType);
        }

        if (severity is not null)
        {
            query = query.Where(item => item.Severity == severity);
        }

        if (onlyUnacknowledged == true)
        {
            query = query.Where(item => !item.IsAcknowledged);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.IsAcknowledged)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SecurityAlertResponse(
                item.Id,
                item.AlertType,
                item.Severity,
                item.Title,
                item.Description,
                item.RelatedUserId,
                item.RelatedUsername,
                item.RelatedIpAddress,
                item.EventCount,
                item.IsAcknowledged,
                item.AcknowledgedByUserId,
                item.AcknowledgedAtUtc,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<SecurityAlertResponse>(items, page, pageSize, total));
    }

    [HttpGet("alerts/{id:guid}")]
    public async Task<ActionResult<SecurityAlertResponse>> GetAlert(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.SecurityAlerts
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new SecurityAlertResponse(
                candidate.Id,
                candidate.AlertType,
                candidate.Severity,
                candidate.Title,
                candidate.Description,
                candidate.RelatedUserId,
                candidate.RelatedUsername,
                candidate.RelatedIpAddress,
                candidate.EventCount,
                candidate.IsAcknowledged,
                candidate.AcknowledgedByUserId,
                candidate.AcknowledgedAtUtc,
                candidate.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("alerts/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, CancellationToken cancellationToken)
    {
        var acknowledged = await alertService.AcknowledgeAsync(id, User.GetUserId(), cancellationToken);
        if (!acknowledged)
        {
            return NotFound();
        }

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.AlertAcknowledged,
                SecuritySeverity.Info,
                "Succeeded",
                "Usuario reconocio una alerta de seguridad.",
                UserId: User.GetUserId(),
                StatusCode: StatusCodes.Status204NoContent,
                ResourceType: "SecurityAlert",
                ResourceId: id.ToString()),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<SecurityDashboardSummaryResponse>> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-10);
        var totalEvents = await dbContext.SecurityEvents.CountAsync(cancellationToken);
        var highOrCriticalEvents = await dbContext.SecurityEvents.CountAsync(
            item => item.Severity == SecuritySeverity.High ||
                    item.Severity == SecuritySeverity.Critical ||
                    item.Severity == SecuritySeverity.Error,
            cancellationToken);
        var openAlerts = await dbContext.SecurityAlerts.CountAsync(item => !item.IsAcknowledged, cancellationToken);
        var loginFailuresLast10 = await dbContext.SecurityEvents.CountAsync(
            item => item.EventType == SecurityEventType.LoginFailed && item.CreatedAtUtc >= since,
            cancellationToken);
        var accessDeniedLast10 = await dbContext.SecurityEvents.CountAsync(
            item => item.EventType == SecurityEventType.AccessDenied && item.CreatedAtUtc >= since,
            cancellationToken);

        return Ok(new SecurityDashboardSummaryResponse(
            totalEvents,
            highOrCriticalEvents,
            openAlerts,
            loginFailuresLast10,
            accessDeniedLast10,
            DateTimeOffset.UtcNow));
    }

    [HttpGet("observability-readiness")]
    public IActionResult GetObservabilityReadiness()
    {
        return Ok(new
        {
            version = "fixed",
            persistedSecurityEvents = true,
            generatedSecurityAlerts = true,
            correlationId = true,
            jwtFailureLogging = true,
            accessDeniedLogging = true,
            sensitiveAdminActionLogging = true,
            externalNotifications = "No implementadas a proposito: la demo usa alertas internas persistidas en SQL Server."
        });
    }
}
