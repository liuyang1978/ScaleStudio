using Avalonia;
namespace ScaleStudio.PrintDesigner;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.App.DesignerOnly=true;
        if(OperatingSystem.IsWindows())App.App.PagePrinter=new Desktop.WindowsPagePrinter();
        AppBuilder.Configure<App.App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}
