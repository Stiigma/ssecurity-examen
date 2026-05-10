using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamenSecurity.Api.Services;

public interface ISecurityLogIntegrityService
{
    Task<string> ComputeHashAsync(SecurityEvent securityEvent, string? previousHash, CancellationToken cancellationToken = default);
    Task<IntegrityCheckResult> VerifyChainAsync(DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, CancellationToken cancellationToken = default);
}

public sealed class SecurityLogIntegrityService(
    IOptions<LogIntegrityOptions> options,
    AppDbContext dbContext,
    ILogger<SecurityLogIntegrityService> logger) : ISecurityLogIntegrityService
{
    private readonly LogIntegrityOptions _options = options.Value;

    public async Task<string> ComputeHashAsync(SecurityEvent securityEvent, string? previousHash, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return "INTEGRITY_DISABLED";
        }

        var keyBytes = Encoding.UTF8.GetBytes(_options.HmacSecretKey);
        using var hmac = new HMACSHA512(keyBytes);

        var hashInput = BuildHashInput(securityEvent, previousHash);
        var inputBytes = Encoding.UTF8.GetBytes(hashInput);
        var hashBytes = hmac.ComputeHash(inputBytes);
        var hashHex = Convert.ToHexString(hashBytes);

        return await Task.FromResult(hashHex);
    }

    public async Task<IntegrityCheckResult> VerifyChainAsync(DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new IntegrityCheckResult(false, 0, null, [], null, "Integrity checks are disabled.");
        }

        var query = dbContext.SecurityEvents.AsNoTracking().OrderBy(e => e.CreatedAtUtc).AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc <= toUtc.Value);
        }

        var events = await query.ToListAsync(cancellationToken);
        var totalChecked = events.Count;

        if (totalChecked == 0)
        {
            return new IntegrityCheckResult(true, 0, null, [], null, "No events to check.");
        }

        var missingEventIds = new List<Guid>();
        Guid? firstBrokenEventId = null;
        string? firstBrokenReason = null;

        for (var i = 0; i < events.Count; i++)
        {
            var evt = events[i];

            // Check for gaps using previous hash continuity
            if (i > 0)
            {
                var previousEvent = events[i - 1];
                if (evt.PreviousHash != previousEvent.EventHash)
                {
                    firstBrokenEventId ??= evt.Id;
                    firstBrokenReason ??= $"PreviousHash mismatch at event {evt.Id}. Expected {previousEvent.EventHash}, got {evt.PreviousHash}.";
                    missingEventIds.Add(evt.Id);
                    logger.LogWarning("Integrity gap detected at event {EventId}. PreviousHash does not match previous EventHash.", evt.Id);
                }
            }

            // Recompute hash
            var recomputedHash = await ComputeHashAsync(evt, evt.PreviousHash, cancellationToken);
            if (evt.EventHash != recomputedHash)
            {
                firstBrokenEventId ??= evt.Id;
                firstBrokenReason ??= $"Hash mismatch at event {evt.Id}. Stored: {evt.EventHash}, Computed: {recomputedHash}.";
                logger.LogWarning("Integrity hash mismatch at event {EventId}.", evt.Id);
            }
        }

        var isValid = firstBrokenEventId is null;
        var message = isValid
            ? $"Chain valid. {totalChecked} events checked."
            : $"Chain broken at event {firstBrokenEventId}. Reason: {firstBrokenReason}";

        return new IntegrityCheckResult(
            isValid,
            totalChecked,
            firstBrokenEventId,
            missingEventIds,
            firstBrokenEventId is null ? null : events.First(e => e.Id == firstBrokenEventId).EventHash,
            message);
    }

    private static string BuildHashInput(SecurityEvent securityEvent, string? previousHash)
    {
        var sb = new StringBuilder();
        sb.Append(securityEvent.EventType.ToString()).Append('|');
        sb.Append(securityEvent.Severity.ToString()).Append('|');
        sb.Append(securityEvent.UserId?.ToString() ?? "null").Append('|');
        sb.Append(securityEvent.Username ?? "null").Append('|');
        sb.Append(securityEvent.IpAddress ?? "null").Append('|');
        sb.Append(securityEvent.Path ?? "null").Append('|');
        sb.Append(securityEvent.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|');
        sb.Append(securityEvent.Outcome).Append('|');
        sb.Append(securityEvent.Message).Append('|');
        sb.Append(securityEvent.MetadataJson ?? "null").Append('|');
        sb.Append(securityEvent.CorrelationId).Append('|');
        sb.Append(securityEvent.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append('|');
        sb.Append(previousHash ?? "null");
        return sb.ToString();
    }
}

public sealed record IntegrityCheckResult(
    bool IsValid,
    int TotalEventsChecked,
    Guid? FirstBrokenEventId,
    IReadOnlyList<Guid> MissingEventIds,
    string? ComputedHashVsStoredHash,
    string Message);
