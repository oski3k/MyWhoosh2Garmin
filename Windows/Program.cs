namespace MyWhoosh2Garmin;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var window = new MainForm(args);
        Application.Run(window);
    }
}
