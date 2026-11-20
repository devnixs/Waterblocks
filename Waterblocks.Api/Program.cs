using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Services.ExchangeSimulators;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Sinks.Datadog.Logs;
using Serilog.Sinks.SystemConsole.Themes;

const string DefaultDatadogUrl = "https://http-intake.logs.datadoghq.com";
const string DefaultDatadogService = "waterblocks-api";
const string DefaultDatadogSource = "aspnetcore";

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early so startup failures are logged too.
Log.Logger = CreateBootstrapLogger(builder.Configuration);
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    ConfigureSerilog(loggerConfiguration, context.Configuration, services);
}, preserveStaticLogger: true);

// Add services to the container.

// Configure database
builder.Services.AddDbContext<FireblocksDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(5)));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHostedService<Waterblocks.Api.Services.AutoTransitionService>();
builder.Services.AddHostedService<Waterblocks.Api.Services.ExchangeMirrorWorker>();

// Exchanges are rows of the exchange catalog. One client serves the simulator of every exchange.
builder.Services.AddHttpClient<IExchangeSimulatorClient, ExchangeSimulatorClient>(
    client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<Waterblocks.Api.Services.IExchangeCatalog, Waterblocks.Api.Services.ExchangeCatalog>();
builder.Services.AddSingleton<IExchangeCredentialsCipher, ExchangeCredentialsCipher>();
builder.Services.AddScoped<Waterblocks.Api.Services.IBalanceService, Waterblocks.Api.Services.BalanceService>();
builder.Services.AddScoped<Waterblocks.Api.Services.ITransactionFactory, Waterblocks.Api.Services.TransactionFactory>();
builder.Services.AddScoped<Waterblocks.Api.Services.ITransactionStateService, Waterblocks.Api.Services.TransactionStateService>();
builder.Services.AddScoped<Waterblocks.Api.Services.ICollateralTransactionRunner, Waterblocks.Api.Services.CollateralTransactionRunner>();
builder.Services.AddScoped<Waterblocks.Api.Services.ICollateralTransferService, Waterblocks.Api.Services.CollateralTransferService>();
builder.Services.AddScoped<Waterblocks.Api.Services.IExchangeCollateralLedger, Waterblocks.Api.Services.ExchangeCollateralLedger>();
builder.Services.AddScoped<Waterblocks.Api.Services.IExchangeAccountService, Waterblocks.Api.Services.ExchangeAccountService>();
builder.Services.AddScoped<Waterblocks.Api.Services.IExchangeMirrorProcessor, Waterblocks.Api.Services.ExchangeMirrorProcessor>();
builder.Services.AddScoped<Waterblocks.Api.Infrastructure.IdempotencyContext>();
builder.Services.AddScoped<Waterblocks.Api.Services.IRealtimeNotifier, Waterblocks.Api.Services.RealtimeNotifier>();
builder.Services.AddScoped<Waterblocks.Api.Services.IAdminTransactionService, Waterblocks.Api.Services.AdminTransactionService>();
builder.Services.AddScoped<Waterblocks.Api.Services.IAdminTransactionMapper, Waterblocks.Api.Services.AdminTransactionMapper>();
builder.Services.AddScoped<Waterblocks.Api.Services.IAdminTransactionNotifier, Waterblocks.Api.Services.AdminTransactionNotifier>();
builder.Services.AddScoped<Waterblocks.Api.Services.IAdminTransactionTransitioner, Waterblocks.Api.Services.AdminTransactionTransitioner>();
builder.Services.AddScoped<Waterblocks.Api.Services.IAdminVaultService, Waterblocks.Api.Services.AdminVaultService>();
builder.Services.AddScoped<Waterblocks.Api.Services.ITransactionService, Waterblocks.Api.Services.TransactionService>();
builder.Services.AddScoped<Waterblocks.Api.Services.ITransactionViewService, Waterblocks.Api.Services.TransactionViewService>();
builder.Services.AddScoped<Waterblocks.Api.Services.ITransactionIdResolver, Waterblocks.Api.Services.TransactionIdResolver>();
builder.Services.AddScoped<Waterblocks.Api.Services.IWalletAddressService, Waterblocks.Api.Services.WalletAddressService>();
builder.Services.AddSingleton<Waterblocks.Api.Services.IAddressGenerator, Waterblocks.Api.Services.AddressGenerator>();
builder.Services.AddSingleton<Waterblocks.Api.Services.IAddressValidationService, Waterblocks.Api.Services.AddressValidationService>();
builder.Services.AddScoped<Waterblocks.Api.Infrastructure.WorkspaceContext>();
builder.Services.AddSingleton<Waterblocks.Api.Models.TransactionStateMachine>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminUi", policy =>
    {
        var frontendOrigin = builder.Configuration["FRONTEND_ORIGIN"];
        var origins = new List<string> { "http://localhost:5173", "http://localhost:5174" };
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            origins.Add(frontendOrigin.Trim());
        }

        policy.WithOrigins(origins.ToArray())
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

app.UseMiddleware<Waterblocks.Api.Middleware.HttpTrafficLoggingMiddleware>();

// Add Fireblocks error handling middleware first to catch all exceptions
app.UseMiddleware<Waterblocks.Api.Middleware.FireblocksErrorMapperMiddleware>();

// Add Fireblocks authentication middleware
app.UseMiddleware<Waterblocks.Api.Middleware.FireblocksAuthenticationMiddleware>();

// The inner error mapper turns exceptions into replies, so that the first reply can be stored.
app.UseWhen(
    Waterblocks.Api.Middleware.IdempotencyMiddleware.AppliesTo,
    branch =>
    {
        branch.UseMiddleware<Waterblocks.Api.Middleware.IdempotencyMiddleware>();
        branch.UseMiddleware<Waterblocks.Api.Middleware.FireblocksErrorMapperMiddleware>();
    });

app.UseRouting();

app.UseCors("AdminUi");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<Waterblocks.Api.Hubs.AdminHub>("/hubs/admin");

try
{
    SeedData.SeedDatabase(app.Services, app.Logger);

    if (!string.IsNullOrWhiteSpace(GetDatadogApiKey(app.Configuration)))
    {
        Log.Information("Datadog log shipping enabled");
    }

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

static Serilog.ILogger CreateBootstrapLogger(IConfiguration configuration)
{
    var loggerConfiguration = new LoggerConfiguration();
    ConfigureSerilog(loggerConfiguration, configuration);
    return loggerConfiguration.CreateBootstrapLogger();
}

static void ConfigureSerilog(
    LoggerConfiguration loggerConfiguration,
    IConfiguration configuration,
    IServiceProvider? services = null)
{
    loggerConfiguration
        .ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(theme: AnsiConsoleTheme.Code);

    if (services is not null)
    {
        loggerConfiguration.ReadFrom.Services(services);
    }

    ConfigureDatadogSink(loggerConfiguration, configuration);
}

static void ConfigureDatadogSink(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
{
    var apiKey = GetDatadogApiKey(configuration);
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return;
    }

    var tags = configuration.GetSection("Datadog:Tags").Get<string[]>() ?? [];
    loggerConfiguration.WriteTo.DatadogLogs(
        apiKey: apiKey,
        source: configuration["Datadog:Source"] ?? DefaultDatadogSource,
        service: configuration["Datadog:Service"] ?? DefaultDatadogService,
        host: Environment.MachineName,
        tags: tags,
        configuration: new DatadogConfiguration(url: configuration["Datadog:Url"] ?? DefaultDatadogUrl));
}

static string? GetDatadogApiKey(IConfiguration configuration)
{
    return FirstNonEmpty(
        configuration["Datadog:ApiKey"],
        configuration["DATADOG_API_KEY"],
        configuration["DD_API_KEY"]);
}

static string? FirstNonEmpty(params string?[] values)
{
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }
    }

    return null;
}
