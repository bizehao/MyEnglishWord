using LevelDB;
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
using Windows.Storage;
using Windows.UI;
using WordNetSqlite;

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

    public static UIElement? AppTitlebar
    {
        get; set;
    }

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

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();


            // Other Activation Handlers
            services.AddSingleton<PlayVoice>();
            //var options = new Options { CreateIfMissing = true };
            //var db = new DB(options, @"C:\Users\bzh\Documents\MyDB\my_english_word.db");
            //services.AddSingleton<DB>(db);

            services.AddDbContext<EnglishWordContext>(async options =>
            {
                var db = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/english_words_lite.db"));
                options.UseSqlite($"Data Source={db.Path}");
            });

            services.AddDbContext<MyWordRecordContext>(async options =>
            {
                var db = "C:/Users/bzh/Documents/MyDB/my_word_record.db";
                options.UseSqlite($"Data Source={db}");
            });

            //初始化wordnet db
            services.AddSingleton<WordNetDatabase>((IServiceProvider sp) =>
            {
                var wordnetdb = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/wordnet31.db"));
                var database = new WordNetDatabase(wordnetdb.Get().Path);
                return database;
            });

            // Views and ViewModels
            services.AddSingleton<SettingsViewModel>();
            services.AddTransient<SettingsPage>();

            services.AddSingleton<StudyStateViewModel>();
            services.AddTransient<StudyStatePage>();

            services.AddSingleton<DBManagerViewModel>();
            services.AddTransient<DBManagerPage>();

            services.AddSingleton<MyWordRecordViewModel>();
            services.AddTransient<MyWordRecordPage>();

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
        var englishWordContext = App.GetService<EnglishWordContext>();
        await englishWordContext.Database.EnsureCreatedAsync();

        var myWordRecordContext = App.GetService<MyWordRecordContext>();
        await myWordRecordContext.Database.EnsureCreatedAsync();

        await App.GetService<IActivationService>().ActivateAsync(args);
        await App.GetService<SettingsViewModel>().InitAsync();

        await App.GetService<WordNetDatabase>().InitializeAsync();

        base.OnLaunched(args);

    }
}
