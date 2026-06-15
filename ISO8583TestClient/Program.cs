using System;
using System.Windows.Forms;

namespace ISO8583TestClient;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ClientForm());
    }
}
