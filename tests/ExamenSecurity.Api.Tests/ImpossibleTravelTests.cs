using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExamenSecurity.Api.Tests;

public class ImpossibleTravelTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ImpossibleTravelTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginFromMexicoThenArgentina_10MinutesApart_GeneratesImpossibleTravelAlert()
    {
        // Arrange
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var email = "student1@demo.local";
        var password = "Student123!";

        // Act: first login from Mexico
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "189.0.0.1");
        var response1 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response1.EnsureSuccessStatusCode();

        // Modify the saved location to be 10 minutes ago
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var location = await dbContext.UserLoginLocations
                .OrderByDescending(l => l.LoginAtUtc)
                .FirstAsync();
            location.LoginAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
            await dbContext.SaveChangesAsync();
        }

        // Second login from Argentina
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "181.0.0.1");
        var response2 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response2.EnsureSuccessStatusCode();

        // Assert
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var locations = await dbContext.UserLoginLocations
                .Where(l => l.IpAddress == "189.0.0.1" || l.IpAddress == "181.0.0.1")
                .ToListAsync();
            Assert.Equal(2, locations.Count);

            var travelEvent = await dbContext.SecurityEvents
                .OrderByDescending(e => e.CreatedAtUtc)
                .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.ImpossibleTravelDetected);

            Assert.NotNull(travelEvent);
            Assert.Contains("Mexico", travelEvent.Message);
            Assert.Contains("Argentina", travelEvent.Message);

            var alert = await dbContext.SecurityAlerts
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefaultAsync(a => a.AlertType == SecurityAlertType.ImpossibleTravel);

            Assert.NotNull(alert);
            Assert.Equal(SecuritySeverity.High, alert.Severity);
            Assert.Contains("Mexico", alert.Description);
            Assert.Contains("Argentina", alert.Description);
        }
    }

    [Fact]
    public async Task LoginFromMexicoThenMadrid_24HoursApart_DoesNotGenerateAlert()
    {
        // Arrange
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var email = "student2@demo.local";
        var password = "Student123!";

        // Act: first login from Mexico
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "189.0.0.1");
        var response1 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response1.EnsureSuccessStatusCode();

        // Modify the saved location to be 24 hours ago
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var location = await dbContext.UserLoginLocations
                .OrderByDescending(l => l.LoginAtUtc)
                .FirstAsync();
            location.LoginAtUtc = DateTimeOffset.UtcNow.AddHours(-24);
            await dbContext.SaveChangesAsync();
        }

        // Second login from Spain (Madrid)
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "46.0.0.1");
        var response2 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response2.EnsureSuccessStatusCode();

        // Assert
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var locations = await dbContext.UserLoginLocations
                .Where(l => l.IpAddress == "189.0.0.1" || l.IpAddress == "46.0.0.1")
                .ToListAsync();
            Assert.Equal(2, locations.Count);

            var travelEvent = await dbContext.SecurityEvents
                .OrderByDescending(e => e.CreatedAtUtc)
                .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.ImpossibleTravelDetected);

            Assert.Null(travelEvent);

            var alert = await dbContext.SecurityAlerts
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefaultAsync(a => a.AlertType == SecurityAlertType.ImpossibleTravel);

            Assert.Null(alert);
        }
    }

    [Fact]
    public async Task LoginFromLocalhostTwice_IgnoredWhenConfigured()
    {
        // Arrange
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var email = "student1@demo.local";
        var password = "Student123!";

        // Act: two logins from localhost
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");

        var response1 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response1.EnsureSuccessStatusCode();

        var response2 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response2.EnsureSuccessStatusCode();

        // Assert
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.FirstAsync(u => u.Email == email);

            var locations = await dbContext.UserLoginLocations
                .Where(l => l.UserId == user.Id)
                .ToListAsync();

            // Local IPs should be ignored, so no locations should be stored
            Assert.Empty(locations);

            var travelEvent = await dbContext.SecurityEvents
                .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.ImpossibleTravelDetected && e.UserId == user.Id);
            Assert.Null(travelEvent);
        }
    }

    [Fact]
    public async Task FirstLogin_DoesNotGenerateImpossibleTravelAlert()
    {
        // Arrange
        var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var email = "student2@demo.local";
        var password = "Student123!";

        // Act: first ever login from Mexico
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "189.0.0.1");
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        // Assert
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.FirstAsync(u => u.Email == email);

            var locations = await dbContext.UserLoginLocations
                .Where(l => l.UserId == user.Id)
                .ToListAsync();
            Assert.Single(locations);

            var travelEvent = await dbContext.SecurityEvents
                .FirstOrDefaultAsync(e => e.EventType == SecurityEventType.ImpossibleTravelDetected && e.UserId == user.Id);
            Assert.Null(travelEvent);
        }
    }
}
