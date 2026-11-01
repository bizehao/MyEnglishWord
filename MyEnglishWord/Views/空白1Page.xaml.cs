using Microsoft.UI.Xaml.Controls;

using MyEnglishWord.ViewModels;

namespace MyEnglishWord.Views;

public sealed partial class 空白1Page : Page
{
    public 空白1ViewModel ViewModel
    {
        get;
    }

    public 空白1Page()
    {
        ViewModel = App.GetService<空白1ViewModel>();
        InitializeComponent();
    }

    private void OnCellContextFlyoutOpening(object sender, WinUI.TableView.TableViewCellContextFlyoutEventArgs e)
    {

    }
}
