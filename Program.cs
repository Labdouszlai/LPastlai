using System.Reflection;

namespace lpastlai;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _ = new HiddenHostForm();
        Application.Run();
    }

    public static Icon LoadAppIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("lpastlai.app.ico");
        return new Icon(stream!);
    }
}
