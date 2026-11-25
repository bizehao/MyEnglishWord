using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Models;
using MyEnglishWord.ViewModels;
using static MyEnglishWord.ViewModels.MyWordRecordViewModel;

namespace MyEnglishWord.Views;

public sealed partial class MyWordRecordPage : Page
    , IRecipient<AddDictDataChanged>
    , IRecipient<DictDataChanged>
{
    public MyWordRecordViewModel ViewModel
    {
        get;
    }

    public MyWordRecordPage()
    {
        ViewModel = App.GetService<MyWordRecordViewModel>();
        InitializeComponent();
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.IsActive = true; // 激活，自动注册 Messenger IRecipient
    }

    protected async override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.IsActive = false; // 取消激活！触发 OnDeactivated，自动取消 messenger 订阅
        base.OnNavigatedFrom(e);
    }

    private async void SearchMyWord_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (sender.Text.Length > 0)
        {
            await ViewModel.LoadWordData(0, sender.Text);
        }
    }

    private async void SearchMyWord_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (sender.Text == string.Empty)
        {
            if (ViewModel.SearchText != string.Empty)
            {
                await ViewModel.LoadWordData(0, string.Empty);
            }
        }
    }

    private async void Pager_SelectedIndexChanged(PagerControl sender, PagerControlSelectedIndexChangedEventArgs args)
    {
        await ViewModel.LoadWordData(sender.SelectedPageIndex, ViewModel.SearchText);
    }

    private async void MenuFlyoutItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: MyWordRecord myWord } menuFlyoutItem)
        {
            return;
        }

        switch (menuFlyoutItem.Tag)
        {
            case "detail":
                var ns = App.GetService<INavigationService>();
                ns.NavigateTo(typeof(MainViewModel).FullName!, myWord.Word);
                break;
            case "delete":
                var mrc = App.GetService<MyWordRecordContext>();
                await mrc.Delete(myWord.Word);
                await ViewModel.LoadWordData(ViewModel.CurrentPage, ViewModel.SearchText);
                break;
        }
    }

    public async void Receive(AddDictDataChanged message)
    {
        await ViewModel.LoadWordTotalCount();
        await ViewModel.LoadWordData(ViewModel.CurrentPage, ViewModel.SearchText, true);
    }

    public void Receive(DictDataChanged message)
    {
        //todo:  AddDictDataChanged
    }
}
