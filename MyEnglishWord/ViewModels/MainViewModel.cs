using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using LevelDB;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.UI.Xaml.Controls;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;
using Windows.Globalization;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using WinRT;
using static System.Net.Mime.MediaTypeNames;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace MyEnglishWord.ViewModels;

public record ShowWordDetailDialog(WordModel word);
public record ShowSpeechRecognizer(WordModel word);

public record AddDictDataChanged();

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    public partial ObservableCollection<WordModel> Items
    {
        get;
        set;
    } = new();

    [ObservableProperty]
    public partial WordModel CurrentWord { get; set; } = new();

    [ObservableProperty]
    public partial WordModel? LikeWordDetail
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsSpeeching
    {
        get;
        set;
    } = false;

    [ObservableProperty]
    public partial bool SpeekInfoBarOpen
    {
        get;
        set;
    } = false;

    [ObservableProperty]
    public partial string SpeekInfoBarMsg
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity SpeekInfoBarSeverity
    {
        get;
        set;
    } = InfoBarSeverity.Informational;

    public MainViewModel()
    {
        
    }

    [RelayCommand]
    private async void HandleShowDetail(WordModel word)
    {
        var myWordRecordContext = App.GetService<MyWordRecordContext>();
        var hasWord = await myWordRecordContext.Has(word.Word);
        word.InDictionary = hasWord;

        LikeWordDetail = word;
        WeakReferenceMessenger.Default.Send(new ShowWordDetailDialog(word));
    }

    [RelayCommand]
    private async void HandlePlayWord(WordModel word)
    {
        var pv = App.GetService<PlayVoice>();
        await pv.Play(word.Word);
    }

    [RelayCommand]
    private async void HandleReadWord(WordModel word)
    {
        WeakReferenceMessenger.Default.Send(new ShowSpeechRecognizer(word));
    }

    [RelayCommand]
    private async void HandleAddDict(WordModel word)
    {
        var db = App.GetService<MyWordRecordContext>();
        await db.Add(word.Word);
        word.InDictionary = true;
        WeakReferenceMessenger.Default.Send(new AddDictDataChanged());
    }

    protected override void OnActivated()
    {
        base.OnActivated();
    }
    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        IsSpeeching = false;
        SpeekInfoBarOpen = false;
    }

}
