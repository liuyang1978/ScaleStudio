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
    public static Func<string, IWeightTransport>? SerialFactory { get; set; }
    public static string DataDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScaleStudio");
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var db = new SqliteDatabase(Path.Combine(DataDirectory, "scale.db"));
        var vm = new MainViewModel(new WeightRecordStore(db),
            new ConfigurationService(new IniConfigurationStore(Path.Combine(DataDirectory,"settings.ini"))),
            SerialFactory);
        var view = new MainView { DataContext = vm };
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Title = "ScaleStudio", Width = 1024, Height = 768,
                MinWidth = 480, MinHeight = 480, Content = view };
            bool closing = false;
            window.Closing += async (_, e) =>
            {
                if (closing) return;
                e.Cancel = true;
                try { await vm.DisposeAsync(); }
                finally { closing = true; window.Close(); }
            };
            desktop.MainWindow = window;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime mobile)
            mobile.MainView = view;
        base.OnFrameworkInitializationCompleted();
    }
}
