using System.Collections.ObjectModel;
using System.Windows.Input;
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
using Windows.ApplicationModel.Chat;
using Windows.Globalization;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;
using WinRT;
using WordNetSqlite;
using static System.Net.Mime.MediaTypeNames;
using static MyEnglishWord.Models.WordModel;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace MyEnglishWord.ViewModels;

//显示单词细节
public record ShowWordDetailDialog(WordModel word);
//展示录音对比
public record ShowSpeechRecognizer(WordModel word);
//添加单词记录
public record AddDictDataChanged(string word);
//跳转到某个搜索
public record JumpToNewSearch(string word);

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

        await AddExtentFromWordNet(word);

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
        WeakReferenceMessenger.Default.Send(new AddDictDataChanged(word.Word));
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

    public static async Task AddExtentFromWordNet(WordModel word)
    {
        if (!word.LoadExtent)
        {
            await FindFromWordNet(word.Word, word.Morphs, word.Synsets);
            word.LoadExtent = true;
        }
    }

    private static async Task FindFromWordNet(string searchStr, ObservableCollection<string> iMorphs, ObservableCollection<SynsetModel> iSynsets)
    {
        //todo:优化写法
        var wordNetDatabase = App.GetService<WordNetDatabase>();
        var baseSynsets = new Dictionary<WordNetSqlite.PartOfSpeech, Synset>();
        var validPos = new WordNetSqlite.PartOfSpeech[] { WordNetSqlite.PartOfSpeech.Noun, WordNetSqlite.PartOfSpeech.Verb, WordNetSqlite.PartOfSpeech.Adj, WordNetSqlite.PartOfSpeech.Adv };
        bool infoFound = false;
        foreach (var pos in validPos)
        {
            var ss = await wordNetDatabase.FindTheInfoDsAsync(searchStr, pos, SearchType.ShowAllSynsetsForWord, 0);
            if (ss != null)
            {
                infoFound = true;
            }
            baseSynsets[pos] = ss;
        }
        var validMorphPos = new WordNetSqlite.PartOfSpeech[] { WordNetSqlite.PartOfSpeech.Noun, WordNetSqlite.PartOfSpeech.Verb, WordNetSqlite.PartOfSpeech.Adj, WordNetSqlite.PartOfSpeech.Adv };
        List<string> morphs = [];
        var morphFound = false;
        foreach (var pos in validMorphPos)
        {
            var morphsOfThisPos = await wordNetDatabase.MorphStrAsync(searchStr, pos);
            if (morphsOfThisPos.Count > 0)
            {
                morphFound = true;
            }
            foreach (var foundMorph in morphsOfThisPos)
            {
                if (!morphs.Contains(foundMorph))
                {
                    morphs.Add(foundMorph);
                }
            }
        }

        if (morphFound && !infoFound && morphs.Count == 1)
        {
            var newSearchStr = morphs[0];
            await FindFromWordNet(newSearchStr, iMorphs, iSynsets);
        }
        else
        {
            foreach (var baseSynset in baseSynsets)
            {
                Synset curr = baseSynset.Value;
                uint indexInThisPos = 0;
                while (curr != null)
                {
                    var synsetModel = new SynsetModel(baseSynset.Key, curr);
                    iSynsets.Add(synsetModel);
                    curr = curr.NextSS!;
                    ++indexInThisPos;
                }
            }

            foreach (var morph in morphs)
            {
                iMorphs.Add(morph);
            }
        }
    }
}
