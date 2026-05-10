using System.Security.Claims;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ExamenSecurity.Api.Options;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HoneytokenController(
    ISecurityAuditService securityAuditService,
    IOptions<HoneytokenOptions> honeytokenOptions,
    ILogger<HoneytokenController> logger) : ControllerBase
{
    [HttpGet("api/internal/backup")]
    [HttpPost("api/internal/backup")]
    public async Task<IActionResult> Backup()
    {
        await HandleHoneytokenAccessAsync("Backup endpoint accessed");
        return Ok(new
        {
            status = "queued",
            jobId = $"job-{Guid.NewGuid():N}",
            estimatedCompletion = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O")
        });
    }

    [HttpGet("api/internal/logs/debug")]
    public async Task<IActionResult> DebugLogs()
    {
        await HandleHoneytokenAccessAsync("Debug logs endpoint accessed");
        return Ok(new
        {
            level = "debug",
            source = "internal-api",
            entries = new[]
            {
                new { timestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"), message = "[HONEYTOKEN] Connection pool initialized" },
                new { timestamp = DateTimeOffset.UtcNow.AddMinutes(-3).ToString("O"), message = "[HONEYTOKEN] Cache warmup completed" }
            }
        });
    }

    [HttpGet("api/admin/config/secrets")]
    public async Task<IActionResult> AdminSecrets()
    {
        await HandleHoneytokenAccessAsync("Admin secrets endpoint accessed");
        return Ok(new
        {
            dbPassword = "[REDACTED_HONEYTOKEN]",
            apiKey = "[HONEYTOKEN_FAKE_KEY]",
            jwtSecret = "[HONEYTOKEN_PLACEHOLDER]",
            awsAccessKey = "AKIAIOSFODNN7EXAMPLE_HONEYTOKEN"
        });
    }

    [HttpGet("api/v1/users/export")]
    public async Task<IActionResult> UsersExport()
    {
        await HandleHoneytokenAccessAsync("Users export endpoint accessed");
        return Ok(new
        {
            status = "processing",
            totalRecords = 9999,
            format = "csv",
            downloadUrl = "/api/v1/users/export/download?token=HONEYTOKEN_FAKE_DOWNLOAD"
        });
    }

    private async Task HandleHoneytokenAccessAsync(string message)
    {
        var options = honeytokenOptions.Value;

        if (options.Enabled && options.EnableDelay)
        {
            var delay = Random.Shared.Next(options.DelayMinMs, options.DelayMaxMs);
            logger.LogDebug("Honeytoken delay: {DelayMs}ms", delay);
            await Task.Delay(delay);
        }

        var body = await ReadRequestBodyTruncatedAsync();
        var queryString = HttpContext.Request.QueryString.ToString();

        await securityAuditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.HoneytokenTriggered,
                SecuritySeverity.Critical,
                "Detected",
                message,
                StatusCode: StatusCodes.Status200OK,
                Metadata: new Dictionary<string, object?>
                {
                    ["method"] = HttpContext.Request.Method,
                    ["path"] = HttpContext.Request.Path,
                    ["queryString"] = string.IsNullOrEmpty(queryString) ? null : queryString,
                    ["bodyTruncated"] = body,
                    ["userAgent"] = HttpContext.Request.Headers.UserAgent.ToString()
                }),
            HttpContext.RequestAborted);
    }

    private async Task<string?> ReadRequestBodyTruncatedAsync()
    {
        if (HttpContext.Request.ContentLength is null or 0)
        {
            return null;
        }

        if (!HttpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            !HttpContext.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) &&
            !HttpContext.Request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            HttpContext.Request.EnableBuffering();
            using var reader = new StreamReader(HttpContext.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            HttpContext.Request.Body.Position = 0;

            const int maxLength = 2000;
            if (body.Length > maxLength)
            {
                body = body[..maxLength] + "...[truncated]";
            }

            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read honeytoken request body");
            return null;
        }
    }
}
