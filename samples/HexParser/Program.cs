using System;
using System.Windows.Forms;

namespace ISO8583HexParser;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ParserForm());
    }
}
