using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace ExamenSecurity.Api.Tests;

public class RateLimitingTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingTests(CustomWebApplicationFactory factory)
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
    public async Task AuthEndpoint_Exceeds5RequestsPerMinute_Returns429()
    {
        // Act: Make 5 requests (should succeed)
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsync("/api/auth/login", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.OK,
                $"Request {i + 1} should not be rate limited. Got: {response.StatusCode}");
        }

        // Act: 6th request should be rate limited
        var blockedResponse = await _client.PostAsync("/api/auth/login", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedResponse.StatusCode);
        Assert.True(blockedResponse.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task GeneralEndpoint_Exceeds100RequestsPerMinute_Returns429()
    {
        // Act: Make 100 requests to a general endpoint
        for (int i = 0; i < 100; i++)
        {
            var response = await _client.GetAsync("/api/demo/scenarios");
            Assert.True(
                response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized,
                $"Request {i + 1} should not be rate limited. Got: {response.StatusCode}");
        }

        // Act: 101st request should be rate limited
        var blockedResponse = await _client.GetAsync("/api/demo/scenarios");

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedResponse.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_Exceeds20RequestsPerMinute_Returns429()
    {
        // Act: Make 20 requests to an admin endpoint
        for (int i = 0; i < 20; i++)
        {
            var response = await _client.GetAsync("/api/admin/users");
            // Admin endpoints require auth, so we expect 401, not 429
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Act: 21st request should be rate limited (429 takes precedence or we check)
        var blockedResponse = await _client.GetAsync("/api/admin/users");

        // Assert: Should be 429 because rate limiting runs before auth
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedResponse.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_IsNotRateLimited()
    {
        // Act: Make many requests to health endpoint
        for (int i = 0; i < 10; i++)
        {
            var response = await _client.GetAsync("/api/demo/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task WhitelistedIp_BypassesRateLimiting()
    {
        // Arrange: Create a client that sends X-Forwarded-For: 127.0.0.1
        // 127.0.0.1 is whitelisted by default in appsettings.json
        var whitelistedClient = _factory.CreateClient();
        whitelistedClient.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");

        // Act: Make many auth requests simulating whitelisted IP
        for (int i = 0; i < 10; i++)
        {
            var response = await whitelistedClient.PostAsync("/api/auth/login", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            // If whitelisted, we should NOT get 429
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task RateLimitExceededResponse_ContainsRetryAfterHeader()
    {
        // Arrange: Exhaust the auth limit
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsync("/api/auth/login", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        }

        // Act
        var response = await _client.PostAsync("/api/auth/login", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        var retryAfter = response.Headers.GetValues("Retry-After").FirstOrDefault();
        Assert.NotNull(retryAfter);
        Assert.True(int.TryParse(retryAfter, out var seconds));
        Assert.True(seconds > 0);
    }

    [Fact]
    public async Task SuccessfulResponse_ContainsRateLimitHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/demo/scenarios");

        // Assert
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
        Assert.True(response.Headers.Contains("X-RateLimit-Reset"));

        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.NotNull(limit);
        Assert.True(int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public async Task RateLimiting_IsConfigurableViaAppsettings()
    {
        // This test verifies that the rate limiting middleware respects configuration.
        // We create a factory with custom (very low) limits to prove configurability.
        var customFactory = new CustomWebApplicationFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["RateLimiting:General:MaxRequests"] = "2",
            ["RateLimiting:General:WindowSeconds"] = "60"
        });

        customFactory.ClearCache();
        var client = customFactory.CreateClient();

        // Act
        var r1 = await client.GetAsync("/api/demo/scenarios");
        var r2 = await client.GetAsync("/api/demo/scenarios");
        var r3 = await client.GetAsync("/api/demo/scenarios");

        // Assert
        Assert.True(r1.StatusCode != HttpStatusCode.TooManyRequests);
        Assert.True(r2.StatusCode != HttpStatusCode.TooManyRequests);
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
    }
}
