using System;
using System.Threading.Tasks;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;

using ISO8583Service.Handlers;
using ISO8583Service.HealthChecks;
using ISO8583Service.Tracing;

namespace ISO8583Service;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/iso8583-service-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Information()
            .CreateLogger();

        try
        {
            Log.Information("─── ISO 8583 Service with WebAPI starting ───");

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog();

            builder.Services.Configure<ServerOptions>(
                builder.Configuration.GetSection(ServerOptions.SectionName));

            builder.Services.Configure<PipelineOptions>(
                builder.Configuration.GetSection("Iso8583Pipeline"));

            // Register PipelineOptions as a direct injectable singleton
            builder.Services.AddSingleton(sp =>
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PipelineOptions>>().Value);

            // ── Pipeline handlers ──────────────────────────────────────────
            // Catch-all (1800 → 1814 echo; unknown MTIs → passthrough)
            builder.Services.AddSingleton<IMessageHandler, DefaultHandler>();

            // Network management (1804 → 1814; logon/logoff/key-change/echo)
            builder.Services.AddSingleton<IMessageHandler, NetworkManagementHandler>();

            // Authorization (1100 → 1110)
            builder.Services.AddSingleton<IMessageHandler, AuthorizationHandler>();
            builder.Services.AddSingleton<IMessageHandler, AuthorizationAdviceHandler>();

            // Financial (1200 → 1210)
            builder.Services.AddSingleton<IMessageHandler, FinancialHandler>();
            builder.Services.AddSingleton<IMessageHandler, FinancialAdviceHandler>();

            // Reversal (1400 → 1410)
            builder.Services.AddSingleton<IMessageHandler, ReversalHandler>();
            builder.Services.AddSingleton<IMessageHandler, ReversalAdviceHandler>();

            // Message tracing — configurable provider
            var traceEnabled = string.Equals(
                builder.Configuration["MessageTrace:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            var traceProvider = builder.Configuration["MessageTrace:Provider"];

            if (traceEnabled && traceProvider == "PostgreSQL")
            {
                // EF Core + Npgsql — persists every ISO 8583 message to PostgreSQL
                builder.Services.AddSingleton<IMessageTracer, EfMessageTracer>();
                builder.Services.AddDbContext<MessageTraceDbContext>(options =>
                    options.UseNpgsql(
                        builder.Configuration["ConnectionStrings:MessageTraceDb"]));
            }
            else
            {
                // File-based tracer via Serilog (zero external dependencies)
                builder.Services.AddSingleton<IMessageTracer, FileMessageTracer>();
            }

            // Pipeline infrastructure
            builder.Services.AddSingleton<HandlerRegistry>();
            builder.Services.AddSingleton<PipelineHost>();

            // Server
            builder.Services.AddSingleton<IIso8583Server, Iso8583TcpServer>();
            builder.Services.AddHostedService<PeriodicSignOnService>();
            builder.Services.AddHostedService<Iso8583HostedService>();
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.AddHealthChecks()
                .AddCheck<PipelineHealthCheck>("pipeline", tags: new[] { "pipeline" });

            var app = builder.Build();

            app.MapControllers();
            app.MapOpenApi();
            app.MapScalarApiReference();
            app.MapHealthChecks("/health");

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Service terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
