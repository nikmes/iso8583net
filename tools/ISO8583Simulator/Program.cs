using ISO8583Net.Packager;
using ISO8583Net.Simulator;
using ISO8583Net.Simulator.Builders;
using ISO8583Net.Simulator.Hubs;
using ISO8583Net.Simulator.Scenarios;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;

// CLI flags:
//   --urls <url>        Override listening URL (e.g., --urls http://0.0.0.0:5100)
// ASP.NET Core automatically binds --urls to Kestrel's listening address.
var builder = WebApplication.CreateBuilder(args);

// ── Config ─────────────────────────────────────────────────────
builder.Services.Configure<SimulatorOptions>(
    builder.Configuration.GetSection(SimulatorOptions.SectionName));

// Register SimulatorOptions directly so SimulatorSession can inject it
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<SimulatorOptions>>().Value);

// ── ISO 8583 packager ──────────────────────────────────────────
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<SimulatorOptions>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<ISOMessagePackager>();
    return new ISOMessagePackager(logger, options.DialectPath);
});

// ── Core simulator services ────────────────────────────────────
builder.Services.AddSingleton<SimulatorSession>();
builder.Services.AddSingleton<MessageHistory>();
builder.Services.AddHostedService<SimulatorHostedService>();

// ── Message builders ───────────────────────────────────────────
builder.Services.AddSingleton<IMessageBuilder, NetworkManagementBuilder>();
builder.Services.AddSingleton<NetworkManagementBuilder>();
builder.Services.AddSingleton<IMessageBuilder, AuthorizationBuilder>();
builder.Services.AddSingleton<AuthorizationBuilder>();
builder.Services.AddSingleton<IMessageBuilder, AuthorizationAdviceBuilder>();
builder.Services.AddSingleton<AuthorizationAdviceBuilder>();
builder.Services.AddSingleton<IMessageBuilder, FinancialBuilder>();
builder.Services.AddSingleton<FinancialBuilder>();
builder.Services.AddSingleton<IMessageBuilder, FinancialAdviceBuilder>();
builder.Services.AddSingleton<FinancialAdviceBuilder>();
builder.Services.AddSingleton<IMessageBuilder, ReversalBuilder>();
builder.Services.AddSingleton<ReversalBuilder>();
builder.Services.AddSingleton<IMessageBuilder, ReversalAdviceBuilder>();
builder.Services.AddSingleton<ReversalAdviceBuilder>();
builder.Services.AddSingleton<MessageBuilderRegistry>();

// ── Scenarios ──────────────────────────────────────────────────
builder.Services.AddSingleton<IScenario, SignOnScenario>();
builder.Services.AddSingleton<IScenario, EchoScenario>();
builder.Services.AddSingleton<IScenario, AuthorizationScenario>();
builder.Services.AddSingleton<IScenario, FinancialScenario>();
builder.Services.AddSingleton<IScenario, ReversalScenario>();
builder.Services.AddSingleton<IScenario, AuthorizationAdviceScenario>();
builder.Services.AddSingleton<IScenario, FinancialAdviceScenario>();
builder.Services.AddSingleton<IScenario, ReversalAdviceScenario>();
builder.Services.AddSingleton<IScenario, FullLifecycleScenario>();
builder.Services.AddSingleton<IScenario, LoadTestScenario>();
builder.Services.AddSingleton<ScenarioRunner>();

// ── API ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── SignalR (stub — hub will be added in Sprint 5) ─────────────
builder.Services.AddSignalR();

// ── CORS (for Blazor WebUI at localhost:5199) ──────────────────
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5199")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

// ── Serilog ────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();  // Scalar docs at /scalar/v1

app.MapHub<SimulatorHub>("/hubs/simulator");

await app.RunAsync();
