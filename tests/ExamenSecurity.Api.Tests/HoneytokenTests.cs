using System.Net;
using System.Net.Http.Json;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExamenSecurity.Api.Tests;

public class HoneytokenTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HoneytokenTests()
    {
        _factory = new CustomWebApplicationFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["Honeytoken:EnableDelay"] = "false"
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.100");
        _client.DefaultRequestHeaders.Add("User-Agent", "HoneytokenTest/1.0");
        _factory.ClearCache();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Theory]
    [InlineData("GET", "/api/internal/backup")]
    [InlineData("POST", "/api/internal/backup")]
    [InlineData("GET", "/api/internal/logs/debug")]
    [InlineData("GET", "/api/admin/config/secrets")]
    [InlineData("GET", "/api/v1/users/export")]
    public async Task HoneytokenRequest_Returns200_WithFakeData(string method, string path)
    {
        // Act
        var response = method switch
        {
            "GET" => await _client.GetAsync(path),
            "POST" => await _client.PostAsync(path, new StringContent("{}", System.Text.Encoding.UTF8, "application/json")),
            _ => throw new NotSupportedException()
        };

        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(content));
        // Ensure no real secrets are leaked
        Assert.DoesNotContain("DefaultConnection", content);
        Assert.DoesNotContain("LOCAL_DEMO_ONLY_CHANGE_ME", content);
        Assert.DoesNotContain("postgres", content);
    }

    [Theory]
    [InlineData("/api/internal/backup")]
    [InlineData("/api/admin/config/secrets")]
    public async Task HoneytokenRequest_CreatesCriticalEvent(string path)
    {
        // Act
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var allEvents = await dbContext.SecurityEvents.ToListAsync();
        var evt = allEvents
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefault(e => e.EventType == SecurityEventType.HoneytokenTriggered);

        if (evt is null)
        {
            var types = string.Join(", ", allEvents.Select(e => e.EventType.ToString()));
            Assert.Fail($"HoneytokenTriggered event not found. Existing events: {types}");
        }

        Assert.Equal(SecuritySeverity.Critical, evt.Severity);
        Assert.Equal(path, evt.Path);
        Assert.True(!string.IsNullOrEmpty(evt.IpAddress), $"IpAddress is null or empty. Event: {System.Text.Json.JsonSerializer.Serialize(evt)}");
        Assert.True(!string.IsNullOrEmpty(evt.UserAgent), $"UserAgent is null or empty. Event: {System.Text.Json.JsonSerializer.Serialize(evt)}");
        Assert.NotNull(evt.CorrelationId);
    }

    [Fact]
    public async Task HoneytokenRequest_CreatesCriticalAlertImmediately()
    {
        // Arrange
        var path = "/api/v1/users/export";

        // Act
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alert = await dbContext.SecurityAlerts
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(a => a.AlertType == SecurityAlertType.HoneytokenAccessed);

        Assert.NotNull(alert);
        Assert.Equal(SecuritySeverity.Critical, alert.Severity);
        Assert.Contains("senoelo", alert.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HoneytokenRequest_MultipleCalls_CreateMultipleAlerts()
    {
        // Arrange
        var path = "/api/internal/logs/debug";

        // Act: call twice
        var r1 = await _client.GetAsync(path);
        var r2 = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Assert: there should be 2 distinct alerts (no deduplication)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alerts = await dbContext.SecurityAlerts
            .Where(a => a.AlertType == SecurityAlertType.HoneytokenAccessed)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(2)
            .ToListAsync();

        Assert.True(alerts.Count >= 2, "Expected at least 2 honeytoken alerts");
    }

    [Fact]
    public async Task HoneytokenRequest_CapturesQueryStringAndBodyInMetadata()
    {
        // Arrange
        var path = "/api/internal/backup?scan=true";
        var body = "{\"action\":\"trigger\"}";

        // Act
        var response = await _client.PostAsync(path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var evt = await dbContext.SecurityEvents
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.HoneytokenTriggered);

        Assert.NotNull(evt);
        Assert.NotNull(evt.MetadataJson);
        Assert.Contains("scan=true", evt.MetadataJson);
        Assert.Contains("trigger", evt.MetadataJson);
    }

    [Fact]
    public async Task HoneytokenResponse_DoesNotContainRealData()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/config/secrets");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("HONEYTOKEN", content);
        Assert.DoesNotContain("DefaultConnection", content);
        Assert.DoesNotContain("CHANGE_ME", content);
        Assert.DoesNotContain("postgres", content);
    }
}
