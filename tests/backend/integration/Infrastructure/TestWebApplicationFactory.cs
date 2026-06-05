using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Waterblocks.Api.Infrastructure.Db;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that configures the API to use a test database.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

    public TestWebApplicationFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        _connectionString = connectionString;
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_configurationOverrides.Count > 0)
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(_configurationOverrides);
            });
        }

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FireblocksDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext with test database connection string
            services.AddDbContext<FireblocksDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });

        builder.UseEnvironment("Test");
    }
}
