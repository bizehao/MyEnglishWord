using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Common;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyEnglishWord.Contracts.Services;
using MyEnglishWord.Helpers;
using MyEnglishWord.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace MyEnglishWord.Control;

public class QueryableTextCommandBarFlyout : Microsoft.UI.Xaml.Controls.CommandBarFlyout
{
    public ICommand CopyCommand
    {
        get;
    }
    public ICommand QueryCommand
    {
        get;
    }
    public ICommand SelectAllCommand
    {
        get;
    }
    public ICommand SpeechCommand
    {
        get;
    }
    public AppBarButton CopyButton
    {
        get;
    }

    public AppBarButton QueryButton
    {
        get;
    }
    public AppBarButton SpeechButton
    {
        get;
    }
    public AppBarButton SelectAllButton
    {
        get;
    }

    public QueryableTextCommandBarFlyout()
    {
        CopyCommand = new RelayCommand(Copy);
        QueryCommand = new RelayCommand(Query);
        SelectAllCommand = new RelayCommand(SelectAll);
        SpeechCommand = new AsyncRelayCommand(SpeechAsync);

        CopyButton = new AppBarButton()
        {
            Icon = new SymbolIcon(Symbol.Copy),
            Command = CopyCommand,
            IsCompact = true
        };
        QueryButton = new AppBarButton()
        {
            Icon = new SymbolIcon(Symbol.Find),
            Command = QueryCommand,
            IsCompact = true
        };

        SpeechButton = new AppBarButton()
        {
            Icon = new SymbolIcon(Symbol.Volume),
            Command = SpeechCommand,
            IsCompact = true
        };

        SelectAllButton = new AppBarButton()
        {
            Icon = new SymbolIcon(Symbol.SelectAll),
            Command = SelectAllCommand,
            IsCompact = true
        };
        PrimaryCommands.Add(CopyButton);
        PrimaryCommands.Add(QueryButton);
        PrimaryCommands.Add(SelectAllButton);
        PrimaryCommands.Add(SpeechButton);
        //CopyButton = new StandardUICommand(StandardUICommandKind.Copy);

        Opening += QueryableTextCommandBarFlyout_Opening;
    }

    private void QueryableTextCommandBarFlyout_Opening(object? sender, object e)
    {

    }

    private void SelectAll()
    {
        if (Target is TextBlock t1)
        {
            t1.SelectAll();
        }
        else if (Target is RichTextBlock t2)
        {
            t2.SelectAll();
        }
    }

    private void Query()
    {
        string selectedText = string.Empty;
        if (Target is TextBlock e1)
        {
            selectedText = e1.SelectedText;
        }
        else if (Target is RichTextBlock e2)
        {
            selectedText = e2.SelectedText;
        }

        selectedText = selectedText.Trim();
        if (selectedText != string.Empty)
        {
            WeakReferenceMessenger.Default.Send(new JumpToNewSearch(selectedText));
        }
        
        Hide();
    }

    private async Task SpeechAsync()
    {
        string selectedText = string.Empty;
        if (Target is TextBlock e1)
        {
            selectedText = e1.SelectedText;
        }
        else if (Target is RichTextBlock e2)
        {
            selectedText = e2.SelectedText;
        }

        selectedText = selectedText.Trim();
        if (selectedText != string.Empty)
        {
            var pv = App.GetService<PlayVoice>();
            await pv.Play(selectedText);
        }

        Hide();

    }

    private void Copy()
    {
        string selectedText = string.Empty;
        if (Target is TextBlock ele1)
        {
            selectedText = ele1.SelectedText;
        }
        else if (Target is RichTextBlock ele2)
        {
            selectedText = ele2.SelectedText;
        }

        selectedText = selectedText.Trim();
        if (selectedText != string.Empty)
        {
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(selectedText);
            Clipboard.SetContent(dataPackage);
        }
        Hide();
    }
}
