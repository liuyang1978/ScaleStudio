using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ScaleStudio.App.Views;

public sealed partial class MainView : UserControl
{
    public MainView() => AvaloniaXamlLoader.Load(this);
}
