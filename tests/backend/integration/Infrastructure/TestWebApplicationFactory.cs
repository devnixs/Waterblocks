using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waterblocks.Api.Infrastructure.Db;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that configures the API to use a test database.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
