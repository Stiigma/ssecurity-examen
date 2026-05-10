using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExamenSecurity.Api.Tests;

public class LogIntegrityTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LogIntegrityTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _factory.ClearCache();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task IntegrityCheck_AfterEvents_ReturnsValidChain()
    {
        // Arrange: generate some events via login
        var adminToken = await GetAdminTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await adminClient.GetAsync("/api/security/integrity-check");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IntegrityCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.True(result.TotalEventsChecked > 0);
    }

    [Fact]
    public async Task IntegrityCheck_DetectsTampering()
    {
        // Arrange: generate an event
        var adminToken = await GetAdminTokenAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var integrityService = scope.ServiceProvider.GetRequiredService<ISecurityLogIntegrityService>();

        // Ensure at least one event exists
        var evt = await dbContext.SecurityEvents.FirstAsync();

        // Tamper with the event directly
        evt.IpAddress = "TAMPERED_IP";
        await dbContext.SaveChangesAsync();

        // Act
        var result = await integrityService.VerifyChainAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.FirstBrokenEventId);
        Assert.Equal(evt.Id, result.FirstBrokenEventId);
    }

    [Fact]
    public async Task IntegrityCheck_DetectsMissingEvent()
    {
        // Arrange: generate events directly via audit service to ensure chain
        using var scope = _factory.Services.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<ISecurityAuditService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var integrityService = scope.ServiceProvider.GetRequiredService<ISecurityLogIntegrityService>();

        for (int i = 0; i < 5; i++)
        {
            await auditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.LoginFailed,
                    SecuritySeverity.Warning,
                    "Rejected",
                    $"Test event {i}",
                    StatusCode: StatusCodes.Status401Unauthorized),
                CancellationToken.None);
        }

        var events = await dbContext.SecurityEvents.OrderBy(e => e.CreatedAtUtc).ToListAsync();
        Assert.True(events.Count >= 5, "Need at least 5 events to test gaps");

        // Remove a middle event to create a gap
        var middleEvent = events[events.Count / 2];
        dbContext.SecurityEvents.Remove(middleEvent);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await integrityService.VerifyChainAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.MissingEventIds);
    }

    [Fact]
    public async Task IntegrityCheck_Endpoint_AuditsEvent()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await adminClient.GetAsync("/api/security/integrity-check");
        response.EnsureSuccessStatusCode();

        // Assert: verify that an IntegrityCheckPerformed event was created
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditEvent = await dbContext.SecurityEvents
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.IntegrityCheckPerformed);

        Assert.NotNull(auditEvent);
        Assert.Equal(SecuritySeverity.Info, auditEvent.Severity);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@demo.local",
            password = "Admin123!"
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        return result.AccessToken;
    }
}
