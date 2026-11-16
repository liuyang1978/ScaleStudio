using Avalonia;

namespace ScaleStudio.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows()) App.App.PagePrinter = new WindowsPagePrinter();
        App.App.DesignerOnly = args.Contains("--designer");
        App.App.SerialFactory = port => new SerialWeightTransport(new SerialOptions(port));
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>().UsePlatformDetect().LogToTrace();
}

