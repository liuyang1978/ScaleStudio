using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace ScaleStudio.Android;

[Activity(Label="ScaleStudio", MainLauncher=true, Exported=true,
    ConfigurationChanges=ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<App.App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        App.App.DataDirectory = FilesDir!.AbsolutePath;
        base.OnCreate(savedInstanceState);
    }
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}
