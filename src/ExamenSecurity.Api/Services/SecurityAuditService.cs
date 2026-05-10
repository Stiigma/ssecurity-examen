using System.Security.Claims;
using System.Text.Json;
using ExamenSecurity.Api;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class SecurityAuditService(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ISecurityAlertService alertService,
    ISecurityLogIntegrityService integrityService,
    IExternalLogForwarder externalLogForwarder,
    ILogger<SecurityAuditService> logger) : ISecurityAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SensitiveMetadataKeys =
    [
        "password",
        "token",
        "authorization",
        "secret",
        "signingkey",
        "credential"
    ];

    public async Task AuditAsync(SecurityAuditRequest request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var principal = httpContext?.User;
        var now = DateTimeOffset.UtcNow;

        var userId = request.UserId ?? principal?.GetUserId();
        if (userId == Guid.Empty)
        {
            userId = null;
        }

        var username = FirstNonEmpty(request.Username, principal?.FindFirstValue(ClaimTypes.Email));
        var role = FirstNonEmpty(request.Role, principal?.FindFirstValue(ClaimTypes.Role));

        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            EventType = request.EventType,
            Severity = request.Severity,
            UserId = userId,
            Username = Normalize(username),
            Role = Normalize(role),
            IpAddress = GetClientIp(httpContext),
            UserAgent = GetUserAgent(httpContext),
            HttpMethod = Truncate(httpContext?.Request.Method, 16),
            Path = Truncate(httpContext?.Request.Path.Value, 512),
            StatusCode = request.StatusCode ?? httpContext?.Response.StatusCode,
            ResourceType = Truncate(request.ResourceType, 120),
            ResourceId = Truncate(request.ResourceId, 120),
            Outcome = Truncate(request.Outcome, 80) ?? "Unknown",
            Message = Truncate(request.Message, 1000) ?? string.Empty,
            MetadataJson = SerializeMetadata(request.Metadata),
            CorrelationId = GetCorrelationId(httpContext),
            CreatedAtUtc = now
        };

        dbContext.SecurityEvents.Add(securityEvent);

        // Log integrity: compute hash chain before saving
        var lastEvent = await dbContext.SecurityEvents
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new { e.EventHash })
            .FirstOrDefaultAsync(cancellationToken);

        var previousHash = lastEvent?.EventHash ?? string.Empty;
        securityEvent.PreviousHash = previousHash;
        securityEvent.EventHash = await integrityService.ComputeHashAsync(securityEvent, previousHash, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        externalLogForwarder.Forward(securityEvent);
        await alertService.EvaluateAsync(securityEvent, cancellationToken);

        logger.LogInformation(
            "Security event persisted. EventType={EventType} Severity={Severity} CorrelationId={CorrelationId}",
            securityEvent.EventType,
            securityEvent.Severity,
            securityEvent.CorrelationId);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetCorrelationId(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue(CorrelationIdMiddleware.ItemName, out var value) == true &&
            value is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    }

    private static string? GetClientIp(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return Truncate(forwardedFor.Split(',')[0].Trim(), 64);
        }

        return Truncate(httpContext.Connection.RemoteIpAddress?.ToString(), 64);
    }

    private static string? GetUserAgent(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        return Truncate(httpContext.Request.Headers["User-Agent"].ToString(), 512);
    }

    private static string? SerializeMetadata(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        var sanitized = metadata.ToDictionary(
            pair => pair.Key,
            pair => ShouldRedact(pair.Key) ? "***redacted***" : pair.Value);

        return Truncate(JsonSerializer.Serialize(sanitized, JsonOptions), 4000);
    }

    private static bool ShouldRedact(string key)
    {
        return SensitiveMetadataKeys.Any(sensitive =>
            key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
