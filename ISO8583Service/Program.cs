using System;
using System.Threading.Tasks;
using ISO8583Net.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            Log.Information("─── ISO 8583 Service starting ───");

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((ctx, services) =>
                {
                    services.Configure<ServerOptions>(
                        ctx.Configuration.GetSection(ServerOptions.SectionName));

                    services.AddSingleton<IIso8583Server, Iso8583TcpServer>();
                    services.AddHostedService<Iso8583HostedService>();
                })
                .Build();

            await host.RunAsync();
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
