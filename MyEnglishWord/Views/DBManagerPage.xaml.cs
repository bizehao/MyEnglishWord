using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Dialog;
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;
using MyEnglishWord.ViewModels;
using Windows.Graphics;
using static MyEnglishWord.ViewModels.MyWordRecordViewModel;

namespace MyEnglishWord.Views;

public sealed partial class DBManagerPage : Page
    , IRecipient<AddDictDataChanged>
{
    public DBManagerViewModel ViewModel
    {
        get;
    }

    private Window? miniWindow;

    public DBManagerPage()
    {
        ViewModel = App.GetService<DBManagerViewModel>();
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

    private void MenuFlyout_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout mf || mf.Target is not Grid { DataContext: WordModel word})
        {
            return;
        }

        listView.SelectedValue = word;

        var menuItem = mf.Items[2] as MenuFlyoutItem;
        if (menuItem != null)
        {
            menuItem.Text = word.InDictionary ? "取消记录" : "添加记录";
        }
    }

    private void ToSee_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: WordModel word })
        {
            return;
        }

        var ns = App.GetService<INavigationService>();
        ns.NavigateTo(typeof(MainViewModel).FullName!, word.Word);

    }

    private async void ToUpdate_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: WordModel word })
        {
            return;
        }
        var wordOpDialog = WordOpDialog.MakeUpdateWordDialog(word);
        wordOpDialog.XamlRoot = XamlRoot;
        await wordOpDialog.ShowAsync();
    }

    private async void Pager_SelectedIndexChanged(PagerControl sender, PagerControlSelectedIndexChangedEventArgs args)
    {
        await ViewModel.LoadWordData(sender.SelectedPageIndex, ViewModel.SearchText);
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

    private async void SearchMyWord_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (sender.Text.Length > 0)
        {
            await ViewModel.LoadWordData(0, sender.Text);
        }
    }

    private void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {

    }

    private async void AddWord_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var wordOpDialog = WordOpDialog.MakeInsertWordDialog();
        wordOpDialog.XamlRoot = XamlRoot;

        await wordOpDialog.ShowAsync();
    }

    public void Receive(AddDictDataChanged message)
    {
        if (ViewModel.Words is not null)
        {
            var it = ViewModel.Words.Find(x => x.Word == message.word);
            if (it is { })
            {
                it.InDictionary = true;
            }
        }
    }

    private async void ToRecord_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: WordModel word })
        {
            return;
        }
        await HandleRecord(word);
    }

    private async void ToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WordModel word })
        {
            return;
        }
        await HandleRecord(word);
    }

    private async Task HandleRecord(WordModel word)
    {
        if (word.InDictionary)
        {
            word.InDictionary = false;
            var myWordRecordContext = App.GetService<MyWordRecordContext>();
            await myWordRecordContext.Delete(word.Word);
        }
        else
        {
            var myWordRecordContext = App.GetService<MyWordRecordContext>();
            word.InDictionary = true;
            await myWordRecordContext.Add(word.Word);
        }

        WeakReferenceMessenger.Default.Send(new DictDataChanged(word.Word, word.InDictionary));
    }

    private void MiniWindow_Click(object sender, RoutedEventArgs e)
    {
        if (miniWindow is null)
        {
            var page = new MiniMainPage();
            if (listView.SelectedValue is not null && listView.SelectedValue is WordModel wordModel)
            {
                page.ViewModel.CurrentWord = wordModel;
            }
            miniWindow = new MiniMainWindow()
            {
                Content = page
            };
            WindowHelper.TrackWindow(miniWindow);

            miniWindow.Closed += (sender, args) =>
            {
                miniWindow = null;
            };

            // ResizeClient takes physical pixels, independent of display scaling.
            miniWindow.AppWindow.ResizeClient(new SizeInt32(800, 600));
        }
 
        miniWindow.Activate();
    }

    private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView lv)
        {
            return;
        }

        if (lv.SelectedValue is not WordModel wordModel)
        {
            return;
        }
        WeakReferenceMessenger.Default.Send(new ShowCurrentWord(wordModel));
    }
}
