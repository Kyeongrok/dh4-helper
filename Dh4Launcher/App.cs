using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dh4Launcher.Forms;
using Dh4Launcher.Forms.Services;
using Dh4Launcher.Forms.UI.Views;
using Dh4Launcher.Forms.ViewModels;

namespace Dh4Launcher;

public class App : Application
{
    private IHost? _host;

    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        ServiceProvider = _host.Services;
        AppServices.Current = _host.Services;

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGameSettingsService, GameSettingsService>();
        services.AddSingleton<IGameLauncherService, GameLauncherService>();
        services.AddSingleton<IGpuService, GpuService>();
        services.AddSingleton<IKeyMappingService, KeyMappingService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
