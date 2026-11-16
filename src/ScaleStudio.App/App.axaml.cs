using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScaleStudio.Configuration;
using ScaleStudio.Storage;
using ScaleStudio.Weighing;
using ScaleStudio.App.Views;
using ScaleStudio.App.ViewModels;

namespace ScaleStudio.App;

public sealed partial class App : Application
{
    public static ScaleStudio.Printing.IPagePrinter? PagePrinter { get; set; }
    public static bool DesignerOnly { get; set; }
    public static Func<string, IWeightTransport>? SerialFactory { get; set; }
    public static string DataDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScaleStudio");
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var logger = new ScaleStudio.Logging.FileLogger(new() { Directory = Path.Combine(DataDirectory, "Logs") });
        var scanner = new ScaleStudio.Scanning.Scanner();
        logger.Log(ScaleStudio.Logging.LogLevel.Info, "Application started");
        scanner.ScanReceived += scan => logger.Log(ScaleStudio.Logging.LogLevel.Info, $"Scan from {scan.DeviceId}: {scan.Text}");
        var db = new SqliteDatabase(Path.Combine(DataDirectory, "scale.db"));
        var vm = new MainViewModel(new WeightRecordStore(db),
            new ConfigurationService(new IniConfigurationStore(Path.Combine(DataDirectory,"settings.ini"))),
            SerialFactory);
        var view = new MainView { DataContext = vm };
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Title = "ScaleStudio", Width = 1024, Height = 768,
                MinWidth = 480, MinHeight = 480, Content = DesignerOnly ? new ScrollViewer { Content = new PrintDesigner() } : view };
            bool closing = false;
            bool capture = false;
            window.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.F8) { capture = !capture; window.Title = capture ? "ScaleStudio — 扫码捕获中（F8退出）" : "ScaleStudio"; e.Handled = true; return; }
                if (!capture) return;
                if (e.Key == Avalonia.Input.Key.Enter) scanner.Feed("keyboard", "\r");
                e.Handled = true;
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            window.AddHandler(Avalonia.Input.InputElement.TextInputEvent, (_, e) =>
            {
                if (!capture) return;
                if (e.Text != null) scanner.Feed("keyboard", e.Text);
                e.Handled = true;
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            window.Closing += async (_, e) =>
            {
                if (closing) return;
                e.Cancel = true;
                try { scanner.Dispose(); await vm.DisposeAsync(); logger.Log(ScaleStudio.Logging.LogLevel.Info, "Application stopping"); await logger.DisposeAsync(); }
                finally { closing = true; window.Close(); }
            };
            desktop.MainWindow = window;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime mobile)
            mobile.MainView = view;
        base.OnFrameworkInitializationCompleted();
    }
}

