using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Models;
using MyEnglishWord.ViewModels;
using MyEnglishWord.Dialog;

namespace MyEnglishWord.Views;

public sealed partial class DBManagerPage : Page
{
    public DBManagerViewModel ViewModel
    {
        get;
    }

    public DBManagerPage()
    {
        ViewModel = App.GetService<DBManagerViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        WeakReferenceMessenger.Default.RegisterAll(this);
        ViewModel.IsActive = true; // 激活，自动注册 Messenger IRecipient
    }

    protected async override void OnNavigatedFrom(NavigationEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        ViewModel.IsActive = false; // 取消激活！触发 OnDeactivated，自动取消 messenger 订阅
        base.OnNavigatedFrom(e);
    }

    private void OnCellContextFlyoutOpening(object sender, WinUI.TableView.TableViewCellContextFlyoutEventArgs e)
    {

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
}
