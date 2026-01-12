using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
builder.Services.AddHostedService<Waterblocks.Api.Services.AutoTransitionService>();
builder.Services.AddScoped<Waterblocks.Api.Services.IBalanceService, Waterblocks.Api.Services.BalanceService>();
builder.Services.AddScoped<Waterblocks.Api.Infrastructure.WorkspaceContext>();
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
app.UseMiddleware<Waterblocks.Api.Middleware.FireblocksErrorMapperMiddleware>();

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
app.UseMiddleware<Waterblocks.Api.Middleware.FireblocksAuthenticationMiddleware>();

app.UseRouting();

app.UseCors("AdminUi");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<Waterblocks.Api.Hubs.AdminHub>("/hubs/admin");

try
{
    SeedData.SeedDatabase(app.Services, app.Logger);

    Log.Information("Starting Waterblocks API");
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

