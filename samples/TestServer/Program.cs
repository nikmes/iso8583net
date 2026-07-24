using System;
using System.Windows.Forms;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ISO8583TestServer;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();

        // Pipeline infrastructure (same as ISO8583Service)
        services.AddSingleton<IMessageHandler, DefaultHandler>();
        services.AddSingleton<HandlerRegistry>();
        services.AddSingleton<PipelineHost>();
        services.AddSingleton<PipelineOptions>(_ => new PipelineOptions());
        services.AddSingleton<ILoggerFactory>(sp =>
        {
            var lf = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug));
            return lf;
        });

        // Server
        services.AddSingleton<IIso8583Server, Iso8583TcpServer>();
        services.AddTransient<MainForm>();

        using var provider = services.BuildServiceProvider();
        var form = provider.GetRequiredService<MainForm>();
        Application.Run(form);
    }
}
