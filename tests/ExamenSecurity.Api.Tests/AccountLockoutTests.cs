using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using ExamenSecurity.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExamenSecurity.Api.Tests;

public class AccountLockoutTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AccountLockoutTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.ClearCache();
    }

    public void Dispose()
    {
        _factory.ClearCache();
    }

    [Fact]
    public async Task After5FailedLogins_AccountIsLocked_Returns423()
    {
        // Arrange: disable rate limiting so it doesn't interfere with lockout
        var customFactory = _factory.WithConfiguration(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "false"
        });
        var client = customFactory.CreateClient();

        var email = "student1@demo.local";
        var password = "WrongPassword123!";

        // Act: 5 failed login attempts
        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Wait for alert evaluation to complete (it runs synchronously in the audit flow)
        // The 6th attempt should be locked
        var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Student123!" });

        // Assert
        Assert.Equal(HttpStatusCode.Locked, lockedResponse.StatusCode);
        var content = await lockedResponse.Content.ReadAsStringAsync();
        Assert.Contains("bloqueada", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminCanViewActiveLockouts()
    {
        // Arrange: lock an account first
        using var scope = _factory.Services.CreateScope();
        var lockoutService = scope.ServiceProvider.GetRequiredService<IAccountLockoutService>();
        await lockoutService.LockAsync(
            LockoutTargetType.User,
            "testlock@demo.local",
            TimeSpan.FromMinutes(15),
            "Test lockout",
            null,
            CancellationToken.None);

        var adminToken = await GetAdminTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await adminClient.GetAsync("/api/security/lockouts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lockouts = await response.Content.ReadFromJsonAsync<List<AccountLockoutResponse>>();
        Assert.NotNull(lockouts);
        Assert.Contains(lockouts, l => l.TargetValue == "testlock@demo.local");
    }

    [Fact]
    public async Task AdminCanUnlockAccount_Manually()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var lockoutService = scope.ServiceProvider.GetRequiredService<IAccountLockoutService>();
        var lockout = await lockoutService.LockAsync(
            LockoutTargetType.User,
            "unlockme@demo.local",
            TimeSpan.FromMinutes(15),
            "Test lockout for unlock",
            null,
            CancellationToken.None);

        Assert.NotNull(lockout);

        var adminToken = await GetAdminTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var unlockResponse = await adminClient.PostAsync($"/api/security/lockouts/{lockout.Id}/unlock", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, unlockResponse.StatusCode);

        // Verify it's unlocked
        var isLocked = await lockoutService.IsLockedAsync("unlockme@demo.local", CancellationToken.None);
        Assert.False(isLocked);
    }

    [Fact]
    public async Task AuditorCannotUnlockAccount_Manually()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var lockoutService = scope.ServiceProvider.GetRequiredService<IAccountLockoutService>();
        var lockout = await lockoutService.LockAsync(
            LockoutTargetType.User,
            "auditor-blocked@demo.local",
            TimeSpan.FromMinutes(15),
            "Test lockout for auditor authorization",
            null,
            CancellationToken.None);

        Assert.NotNull(lockout);

        var auditorToken = await GetAuditorTokenAsync();
        var auditorClient = _factory.CreateClient();
        auditorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auditorToken);

        // Act
        var unlockResponse = await auditorClient.PostAsync($"/api/security/lockouts/{lockout.Id}/unlock", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, unlockResponse.StatusCode);

        var isLocked = await lockoutService.IsLockedAsync("auditor-blocked@demo.local", CancellationToken.None);
        Assert.True(isLocked);
    }

    [Fact]
    public async Task LockedAccount_AttemptGeneratesLockoutAttemptDuringLockEvent()
    {
        // Arrange: use an existing user from the seeder
        var email = "student2@demo.local";
        using var scope = _factory.Services.CreateScope();
        var lockoutService = scope.ServiceProvider.GetRequiredService<IAccountLockoutService>();
        await lockoutService.LockAsync(
            LockoutTargetType.User,
            email,
            TimeSpan.FromMinutes(15),
            "Test lock",
            null,
            CancellationToken.None);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "AnyPassword123!"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);

        // Verify security event was created
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var securityEvent = await dbContext.SecurityEvents
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.LockoutAttemptDuringLock);

        Assert.NotNull(securityEvent);
        Assert.Equal(email, securityEvent.Username);
    }

    [Fact]
    public async Task Lockout_DurationConfigurableViaAppsettings()
    {
        // Arrange
        var customFactory = new CustomWebApplicationFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["AccountLockout:BaseDurationMinutes"] = "1"
        });

        using var scope = customFactory.Services.CreateScope();
        var lockoutService = scope.ServiceProvider.GetRequiredService<IAccountLockoutService>();

        // Act
        var lockout = await lockoutService.LockAsync(
            LockoutTargetType.User,
            "shortlock@demo.local",
            TimeSpan.FromMinutes(1),
            "Short duration test",
            null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(lockout);
        Assert.True(lockout.LockedUntilUtc <= DateTimeOffset.UtcNow.AddMinutes(1).AddSeconds(5));
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

    private async Task<string> GetAuditorTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "auditor@demo.local",
            password = "Auditor123!"
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        return result.AccessToken;
    }
}
