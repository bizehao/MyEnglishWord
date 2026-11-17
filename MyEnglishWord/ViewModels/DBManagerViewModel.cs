using CommunityToolkit.Mvvm.ComponentModel;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Models;

namespace MyEnglishWord.ViewModels;

public partial class DBManagerViewModel : ObservableRecipient
{

    [ObservableProperty]
    public partial List<WordModel>? Words
    {
        get;
        set;
    }

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

    [ObservableProperty]
    public partial string SearchText
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

    public DBManagerViewModel()
    {
    }

    protected async override void OnActivated()
    {
        base.OnActivated();
    }
    protected override void OnDeactivated()
    {
        base.OnDeactivated();
    }

    public async Task LoadWordData(int index, string searchString)
    {
        if (OldDataState == (index, searchString))
        {
            return;
        }
        OldDataState = (index, searchString);

        var mrc = App.GetService<EnglishWordContext>();
        (List<WordEntry> Data, int PageCount) data;
        if (searchString != string.Empty)
        {
            SearchText = searchString;
            data = await mrc.GetDataByStr(SearchText, index, PageSize);
            var tmpWords = data.Data;
            Words = await Task.Run(() =>
            {
                return tmpWords.Select(e => WordModel.MakeWordModel(e)).ToList();
            });
            PageCount = data.PageCount;
        }
        else
        {
            SearchText = string.Empty;
            data = await mrc.GetData(index, PageSize);
            var tmpWords = data.Data;
            Words = await Task.Run(() =>
            {
                return tmpWords.Select(e => WordModel.MakeWordModel(e)).ToList();
            });
            PageCount = data.PageCount;
        }

        CurrentPage = index;
    }
}
