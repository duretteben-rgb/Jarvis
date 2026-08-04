using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Jarvis.UI.ViewModels;
using Jarvis.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.UI;

/// <summary>
/// Avalonia application shell. Hosts the desktop window and resolves the main view model
/// from the JARVIS service provider.
/// </summary>
public partial class App : Application
{
    /// <summary>Service provider of the running JARVIS host, set before the window is shown.</summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel viewModel = ServiceProvider?.GetService<MainViewModel>() ?? new MainViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
