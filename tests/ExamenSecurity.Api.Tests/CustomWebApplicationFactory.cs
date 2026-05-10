using ExamenSecurity.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExamenSecurity.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _overrides = new();
    private readonly Guid _dbId = Guid.NewGuid();

    public CustomWebApplicationFactory WithConfiguration(Dictionary<string, string?> overrides)
    {
        var factory = new CustomWebApplicationFactory();
        foreach (var pair in overrides)
        {
            factory._overrides[pair.Key] = pair.Value;
        }

        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            if (_overrides.Count > 0)
            {
                config.AddInMemoryCollection(_overrides);
            }
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{_dbId}");
            });
        });
    }

    public void ClearCache()
    {
        using var scope = Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        if (cache is MemoryCache memCache)
        {
            memCache.Clear();
        }
    }
}
