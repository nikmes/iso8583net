using System;
using System.Windows.Forms;
using ISO8583Net.Server;
using Microsoft.Extensions.DependencyInjection;

namespace ISO8583TestServer;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        services.AddSingleton<IIso8583Server, Iso8583TcpServer>();
        services.AddTransient<MainForm>();

        using var provider = services.BuildServiceProvider();
        var form = provider.GetRequiredService<MainForm>();
        Application.Run(form);
    }
}
