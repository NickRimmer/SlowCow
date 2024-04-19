using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.UI.ViewModels;
using SlowCow.Setup.UI.Views;
namespace SlowCow.Setup.UI;

internal class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var runnerSettings = Runner.Services.GetRequiredService<RunnerModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow {
                DataContext = new MainWindowViewModel {
                    AppName = runnerSettings.Name,
                    Description = runnerSettings.Description,
                },
            };

            // remove maximize button
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
