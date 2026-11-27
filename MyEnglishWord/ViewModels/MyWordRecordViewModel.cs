using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.UI.Xaml.Controls;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;

namespace MyEnglishWord.ViewModels;

public partial class MyWordRecordViewModel : ObservableRecipient
{

    public record DictDataChanged(string Word, bool Result);

    [ObservableProperty]
    public partial ObservableCollection<MyWordRecord> WordRecords
    {
        get;
        set;
    } = new();

    [ObservableProperty]
    public partial int PageCount
    {
        get;
        set;
    } = 0;

    [ObservableProperty]
    public partial int CurrentPage
    {
        get;
        set;
    } = 0;

    public string SearchText
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial int WordTotalCount
    {
        get;
        set;
    } = 0;

    [ObservableProperty]
    public partial string WordTranslate
    {
        get;
        set;
    } = string.Empty;


    public int PageSize
    {
        get;
        set;
    } = 100;

    private (int index, string searchString) OldDataState = (-1, string.Empty);

    public MyWordRecordViewModel()
    {
    }

    protected async override void OnActivated()
    {
        base.OnActivated();
        await LoadWordTotalCount();
    }
    protected override void OnDeactivated()
    {
        base.OnDeactivated();
    }

    public async Task LoadWordTotalCount()
    {
        var mrc = App.GetService<MyWordRecordContext>();
        WordTotalCount = await mrc.GetTotalCount();
    }

    public async Task LoadWordData(int index, string searchString, bool force = false)
    {
        if (OldDataState == (index, searchString) && !force)
        {
            return;
        }
        OldDataState = (index, searchString);

        var mrc = App.GetService<MyWordRecordContext>();
        if (searchString != string.Empty)
        {
            SearchText = searchString;
            var data = await mrc.GetDataByStr(SearchText, index, PageSize);
            WordRecords.Clear();
            WordRecords.AddRange(data.Records);
            PageCount = data.PageCount;
        }
        else
        {
            SearchText = string.Empty;
            var data = await mrc.GetData(index, PageSize);
            WordRecords.Clear();
            WordRecords.AddRange(data.Records);
            PageCount = data.PageCount;
        }

        CurrentPage = index;
    }

    [RelayCommand]
    private async Task HandlePlayWord(string word)
    {
        var pv = App.GetService<PlayVoice>();
        await pv.Play(word);
    }

    [RelayCommand]
    private async Task HandleWordTranslate(MyWordRecord wordRecord)
    {
        var dbService = App.GetService<EnglishWordContext>();
        var result = await dbService.QueryWordAsync(wordRecord.Word);

        if (result is not null)
        {
            WordTranslate = result.Translation ?? string.Empty;
        }
    }

    
}
