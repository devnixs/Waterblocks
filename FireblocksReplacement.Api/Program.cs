using FireblocksReplacement.Api.Infrastructure.Db;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Replace default logging with Serilog
builder.Host.UseSerilog();

// Add services to the container.

// Configure database
builder.Services.AddDbContext<FireblocksDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminUi", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add Fireblocks error handling middleware first to catch all exceptions
app.UseMiddleware<FireblocksReplacement.Api.Middleware.FireblocksErrorMapperMiddleware>();

// Enable request logging with Serilog
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
    };
});

// Add Fireblocks authentication middleware
app.UseMiddleware<FireblocksReplacement.Api.Middleware.FireblocksAuthenticationMiddleware>();

app.UseRouting();

app.UseCors("AdminUi");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<FireblocksReplacement.Api.Hubs.AdminHub>("/hubs/admin");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
        db.Database.Migrate();
        SeedAssets(db, app.Logger);
    }

    Log.Information("Starting FireblocksReplacement API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static void SeedAssets(FireblocksDbContext db, ILogger logger)
{
    var assetsPath = Path.Combine(AppContext.BaseDirectory, "all_fireblocks_assets.json");
    if (!File.Exists(assetsPath))
    {
        logger.LogWarning("Asset seed file not found at {Path}", assetsPath);
        return;
    }

    var json = File.ReadAllText(assetsPath);
    var allAssets = JsonSerializer.Deserialize<List<FireblocksAssetSeed>>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    }) ?? new List<FireblocksAssetSeed>();

    var requiredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BTC",
        "ETH",
        "ADA",
        "AAVE",
        "AXS",
        "BNB_BSC",
        "BNB_ERC20",
        "BONK_SOL",
        "EUROC_ETH_F5NG",
        "EURR_ETH_YSHA",
        "MATIC",
        "MATIC_POLYGON",
        "SOL",
        "SUSD",
        "USDC",
        "USDC_POLYGON",
        "USDC_POLYGON_NXTB",
        "USDS",
        "USDT_ERC20",
    };

    var byId = allAssets.ToDictionary(a => a.Id, a => a, StringComparer.OrdinalIgnoreCase);
    foreach (var id in requiredIds)
    {
        if (!byId.TryGetValue(id, out var seed))
        {
            logger.LogWarning("Seed asset not found in JSON: {AssetId}", id);
            continue;
        }

        var asset = db.Assets.FirstOrDefault(a => a.AssetId == seed.Id);
        if (asset == null)
        {
            asset = new FireblocksReplacement.Api.Models.Asset
            {
                AssetId = seed.Id,
                CreatedAt = DateTime.UtcNow,
            };
            db.Assets.Add(asset);
        }

        asset.Name = seed.Name ?? seed.Id;
        asset.Type = seed.Type;
        asset.ContractAddress = string.IsNullOrWhiteSpace(seed.ContractAddress) ? null : seed.ContractAddress;
        asset.NativeAsset = seed.NativeAsset;
        asset.Decimals = seed.Decimals ?? 0;
        asset.Symbol = DeriveSymbol(seed.Id);
        asset.IsActive = true;
    }

    db.SaveChanges();
}

static string DeriveSymbol(string assetId)
{
    var idx = assetId.IndexOf('_');
    if (idx <= 0) return assetId.Length > 10 ? assetId[..10] : assetId;
    var symbol = assetId[..idx];
    return symbol.Length > 10 ? symbol[..10] : symbol;
}

sealed class FireblocksAssetSeed
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? ContractAddress { get; set; }
    public int? Decimals { get; set; }
    public string? NativeAsset { get; set; }
}

namespace FireblocksReplacement.Api.Hubs
{
    public class AdminHub : Hub
    {
    }
}
