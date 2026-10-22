using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Shion.Services;
using PCL.Shion.ViewModels;

namespace PCL.Shion;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new GameServices(AppSettings.Load());
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(services),
            };
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
