using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FuelStation.SupplyServiceUI.Infrastructure;
using FuelStation.SupplyServiceUI.ViewModels;
using FuelStation.SupplyServiceUI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FuelStation.SupplyServiceUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serviceProvider = ConfigureServices();
            desktop.MainWindow = serviceProvider.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<AppConfigProvider>();
        
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
        
        services.AddTransient<TankerSelectionViewModel>();
        services.AddTransient<TankerSelectionDialog>();

        return services.BuildServiceProvider();
    }
}