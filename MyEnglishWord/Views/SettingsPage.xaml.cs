using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Defines;
using MyEnglishWord.Helpers;
using MyEnglishWord.Services;
using MyEnglishWord.ViewModels;

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

        var pv = App.GetService<PlayVoice>();
        await pv.Init();
    }

    private async void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var pv = App.GetService<PlayVoice>();
        var it = ViewModel.VoiceChoose[ViewModel.VoiceSelected];
        pv.Reset(it.Name, it.Type);

        var lss = App.GetService<ILocalSettingsService>();
        if (lss is { })
        {
            await lss.SaveSettingAsync(LocalSetDefine.VoiceSelected, it.Name);
        }
    }
}
