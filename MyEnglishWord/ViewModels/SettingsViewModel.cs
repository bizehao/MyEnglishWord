using System.Reflection;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Defines;
using MyEnglishWord.Helpers;

using Windows.ApplicationModel;
using Windows.Media.SpeechSynthesis;

namespace MyEnglishWord.ViewModels;

public record VoiceModel(string Name, PlayVoice.VoiceType Type);
public record MyWordRecordDBChanged(); //todo 刷新其它页面
public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;

    [ObservableProperty]
    public partial ElementTheme ElementTheme
    {
        get; set;
    }

    [ObservableProperty]
    public partial string VersionDescription
    {
        get; set;
    }

    public ICommand SwitchThemeCommand
    {
        get;
    }

    [ObservableProperty]
    public partial int VoiceSelected
    {
        get; set;
    } = 0;

    [ObservableProperty]
    public partial List<VoiceModel> VoiceChoose
    {
        get; set;
    } = new();


    public SettingsViewModel(IThemeSelectorService themeSelectorService)
    {
        _themeSelectorService = themeSelectorService;
        ElementTheme = _themeSelectorService.Theme;
        VersionDescription = GetVersionDescription();

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });

        VoiceChoose.Add(new VoiceModel("Piper Lessac", PlayVoice.VoiceType.Piper));
        VoiceChoose.AddRange(
            SpeechSynthesizer.AllVoices
                .Where(v => v.Language == "en-US")
                .Select(e => new VoiceModel(e.DisplayName, PlayVoice.VoiceType.Microsoft))
                );
    }

    public async Task InitAsync()
    {

        var lss = App.GetService<ILocalSettingsService>();

        var selected = await lss.ReadSettingAsync<string>(LocalSetDefine.VoiceSelected);

        VoiceSelected = VoiceChoose.FindIndex(e => e.Name == selected);

        if (VoiceSelected == -1)
        {
            VoiceSelected = 0;
        }

        //init play voice
        var pv = App.GetService<PlayVoice>();
        var it = VoiceChoose[VoiceSelected];
        pv.Reset(it.Name, it.Type);
        await pv.Init();
    }

    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
