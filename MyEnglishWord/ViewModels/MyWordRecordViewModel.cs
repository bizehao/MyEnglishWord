using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Models;

namespace MyEnglishWord.ViewModels;

public partial class MyWordRecordViewModel : ObservableRecipient
{

    public record DictDataChanged(string Word, bool Result);

    [ObservableProperty]
    public partial List<MyWordRecord>? WordRecords
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
        //await LoadDefaultData();

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
            WordRecords = data.Records;
            PageCount = data.PageCount;
        }
        else
        {
            SearchText = string.Empty;
            var data = await mrc.GetData(index, PageSize);
            WordRecords = data.Records;
            PageCount = data.PageCount;
        }

        CurrentPage = index;
    }
}
