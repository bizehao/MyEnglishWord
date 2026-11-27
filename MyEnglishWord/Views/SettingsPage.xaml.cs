using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.Storage.Pickers;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Defines;
using MyEnglishWord.Helpers;
using MyEnglishWord.Services;
using MyEnglishWord.ViewModels;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MyEnglishWord.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.IsActive = true;
    }

    protected async override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.IsActive = false;
        base.OnNavigatedFrom(e);
    }

    private async void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var pv = App.GetService<PlayVoice>();
        var it = ViewModel.VoiceChoose[ViewModel.VoiceSelected];
        pv.Reset(it.Name, it.Type);
        await pv.Init();

        var lss = App.GetService<ILocalSettingsService>();
        if (lss is { })
        {
            await lss.SaveSettingAsync(LocalSetDefine.VoiceSelected, it.Name);
        }
    }

    private async void Import_Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Create the picker using the AppWindowId from the element
        var picker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        // Add file type filters
        picker.FileTypeFilter.Add(".db");

        // Show picker
        PickFileResult result = await picker.PickSingleFileAsync();

        if (result != null)
        {
            var loadingDialog = MakeLoadingDialog();
            try
            {
                var _ = loadingDialog.ShowAsync();

                var mydb = App.GetService<MyWordRecordContext>();
                await mydb.Database.ExecuteSqlRawAsync(@$"ATTACH DATABASE '{result.Path}' AS src;
                                            INSERT OR IGNORE INTO my_word_record (word, time) SELECT word, time FROM src.my_word_record;
                                            DETACH DATABASE src;
                                            ");
                WeakReferenceMessenger.Default.Send(new MyWordRecordDBChanged());
            }
            catch
            {
                var dialog = new ContentDialog()
                {
                    XamlRoot = XamlRoot,
                    Title = "提示",
                    Content = "数据库格式不对",
                    PrimaryButtonText = "确定",
                    DefaultButton = ContentDialogButton.Primary
                };
                await dialog.ShowAsync();
            }
            finally
            {
                loadingDialog.Hide();
            }
        }
    }

    private async void Export_Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var installPath = Package.Current.InstalledPath;
        var db = System.IO.Path.Combine(installPath, "my_word_record.db");

        var picker = new FileSavePicker(App.MainWindow.AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "export",
            DefaultFileExtension = ".db",
            CommitButtonText = "保存",
        };

        var result = await picker.PickSaveFileAsync();
        if (result != null)
        {
            File.Copy(db, result.Path, overwrite: true);
        }
    }

    private ContentDialog MakeLoadingDialog()
    {
        var ring = new ProgressRing { IsActive = true, Width = 32, Height = 32 };
        var text = new TextBlock { Text = "加载中..." };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { ring, text }
        };

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "请等待",
            Content = panel
        };

        return dlg;
    }
}
