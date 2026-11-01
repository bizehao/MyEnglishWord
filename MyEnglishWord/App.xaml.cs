using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using MyEnglishWord.Activation;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Core.Contracts.Services;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Services;
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;
using MyEnglishWord.Services;
using MyEnglishWord.ViewModels;
using MyEnglishWord.Views;
using Windows.UI;

namespace MyEnglishWord;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers
            services.AddSingleton<PlayVoice>();


            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            services.AddDbContext<EnglishWordContext>(options => {
                options.UseSqlite($"Data Source={"C:\\Users\\bzh\\Downloads\\ECDICT-master\\stardict\\english_words_lite.sqlite"}");
            });

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddSingleton<SettingsViewModel>();
            services.AddTransient<SettingsPage>();

            services.AddSingleton<空白2ViewModel>();
            services.AddTransient<空白2Page>();

            services.AddSingleton<空白1ViewModel>();
            services.AddTransient<空白1Page>();

            services.AddSingleton<空白ViewModel>();
            services.AddTransient<空白Page>();

            services.AddSingleton<MainViewModel>();
            services.AddTransient<MainPage>();

            services.AddSingleton<ShellViewModel>();
            services.AddTransient<ShellPage>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));

        }).
        Build();

        UnhandledException += App_UnhandledException;

    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        await App.GetService<IActivationService>().ActivateAsync(args);
        await App.GetService<SettingsViewModel>().InitAsync();
        

        //触发模型初始化 无效
        var dbContext = App.GetService<EnglishWordContext>();
        await dbContext.Words.AnyAsync();
        //await dbContext.Database.OpenConnectionAsync();
        //await dbContext.Database.CloseConnectionAsync();
    }
}
