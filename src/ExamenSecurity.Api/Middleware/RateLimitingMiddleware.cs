using System.Globalization;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Options;
using ExamenSecurity.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ExamenSecurity.Api.Middleware;

public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IMemoryCache _cache;
    private readonly RateLimitingOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IMemoryCache cache,
        IOptions<RateLimitingOptions> options)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        if (IsPathExcluded(path))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        if (IsWhitelisted(clientIp))
        {
            await _next(context);
            return;
        }

        var rule = ResolveRule(path);
        var windowStart = GetWindowStart(rule.WindowSeconds);
        var cacheKey = $"ratelimit:{clientIp}:{method}:{NormalizePathForKey(path, rule)}:{windowStart:O}";

        var counter = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(rule.WindowSeconds);
            return new RateLimitCounter
            {
                WindowStart = windowStart,
                WindowSeconds = rule.WindowSeconds,
                Limit = rule.MaxRequests,
                Count = 0
            };
        });

        if (counter is null)
        {
            await _next(context);
            return;
        }

        // Thread-safe increment
        var currentCount = Interlocked.Increment(ref counter.Count);
        var resetTime = counter.WindowStart.AddSeconds(counter.WindowSeconds);
        var remaining = Math.Max(0, counter.Limit - currentCount);

        AddRateLimitHeaders(context, counter.Limit, remaining, resetTime);

        if (currentCount > counter.Limit)
        {
            var retryAfter = Math.Max(1, (int)(resetTime - DateTimeOffset.UtcNow).TotalSeconds);
            _logger.LogWarning(
                "Rate limit exceeded for IP {ClientIp} on {Path}. Count={Count}, Limit={Limit}",
                clientIp,
                path,
                currentCount,
                counter.Limit);

            await LogRateLimitEventAsync(context, clientIp, path, currentCount, counter.Limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

            await context.Response.WriteAsJsonAsync(new
            {
                message = "Too many requests. Please try again later.",
                retryAfter,
                correlationId = context.Items[CorrelationIdMiddleware.ItemName]
            });

            return;
        }

        await _next(context);
    }

    private static void AddRateLimitHeaders(HttpContext context, int limit, int remaining, DateTimeOffset resetTime)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
            {
                context.Response.Headers["X-RateLimit-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
            }

            if (!context.Response.Headers.ContainsKey("X-RateLimit-Remaining"))
            {
                context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString(CultureInfo.InvariantCulture);
            }

            if (!context.Response.Headers.ContainsKey("X-RateLimit-Reset"))
            {
                context.Response.Headers["X-RateLimit-Reset"] = resetTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            }

            return Task.CompletedTask;
        });
    }

    private static async Task LogRateLimitEventAsync(
        HttpContext context,
        string clientIp,
        string path,
        int count,
        int limit)
    {
        try
        {
            var auditService = context.RequestServices.GetService<ISecurityAuditService>();
            if (auditService is not null)
            {
                await auditService.AuditAsync(
                    new SecurityAuditRequest(
                        SecurityEventType.RateLimitExceeded,
                        SecuritySeverity.Warning,
                        "RateLimitExceeded",
                        $"Rate limit exceeded for IP {clientIp} on {path}.",
                        StatusCode: StatusCodes.Status429TooManyRequests,
                        Metadata: new Dictionary<string, object?>
                        {
                            ["clientIp"] = clientIp,
                            ["path"] = path,
                            ["count"] = count,
                            ["limit"] = limit
                        }),
                    context.RequestAborted);
            }
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetService<ILogger<RateLimitingMiddleware>>();
            logger?.LogError(ex, "Failed to log rate limit exceeded event.");
        }
    }

    private bool IsPathExcluded(string path)
    {
        foreach (var excluded in _options.ExcludedPaths)
        {
            if (path.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(excluded.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWhitelisted(string clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            return false;
        }

        foreach (var whitelisted in _options.WhitelistedIps)
        {
            if (clientIp.Equals(whitelisted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private RateLimitRule ResolveRule(string path)
    {
        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
        {
            return _options.Auth;
        }

        if (path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase))
        {
            return _options.Admin;
        }

        return _options.General;
    }

    private static string NormalizePathForKey(string path, RateLimitRule rule)
    {
        // Use the full path for auth endpoints (per-endpoint limiting)
        // Use a wildcard bucket for general endpoints to keep keys manageable
        return path;
    }

    private static DateTimeOffset GetWindowStart(int windowSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var windowTicks = windowSeconds * TimeSpan.TicksPerSecond;
        var startTicks = (now.Ticks / windowTicks) * windowTicks;
        return new DateTimeOffset(startTicks, TimeSpan.Zero);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private sealed class RateLimitCounter
    {
        public DateTimeOffset WindowStart;
        public int WindowSeconds;
        public int Limit;
        public int Count;
    }
}
