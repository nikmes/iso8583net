using System;
using System.Threading.Tasks;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;

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

            // Pipeline handler registration (add custom handlers here)
            builder.Services.AddSingleton<IMessageHandler, DefaultHandler>();

            // Pipeline infrastructure
            builder.Services.AddSingleton<HandlerRegistry>();
            builder.Services.AddSingleton<PipelineHost>();

            // Server
            builder.Services.AddSingleton<IIso8583Server, Iso8583TcpServer>();
            builder.Services.AddHostedService<PeriodicSignOnService>();
            builder.Services.AddHostedService<Iso8583HostedService>();
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.MapControllers();
            app.MapOpenApi();
            app.MapScalarApiReference();

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
