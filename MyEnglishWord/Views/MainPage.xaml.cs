using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Models;
using MyEnglishWord.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using WinRT;

namespace MyEnglishWord.Views;

public sealed partial class MainPage : Page
    , IRecipient<ShowWordDetailDialog>
    , IRecipient<ShowSpeechRecognizer>
{
    public MainViewModel ViewModel
    {
        get;
    }

    private DispatcherQueueTimer _timerQueue;

    private readonly SpeechRecognizer _speechRecognizer;

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        _timerQueue = DispatcherQueue.CreateTimer();


        var language = new Language("en-US");
        _speechRecognizer = new SpeechRecognizer(language); // 默认使用系统语音语言
        _speechRecognizer.UIOptions.AudiblePrompt = "请说话";
        _speechRecognizer.UIOptions.ShowConfirmation = true;

    }

    private async void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        //foreach (var word in result)
        //{
        //    System.Diagnostics.Debug.WriteLine(word.Word);
        //}
    }

    private void OnCellContextFlyoutOpening(object sender, WinUI.TableView.TableViewCellContextFlyoutEventArgs e)
    {

    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        WeakReferenceMessenger.Default.RegisterAll(this);
        ViewModel.IsActive = true; // 激活，自动注册 Messenger IRecipient
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        ViewModel.IsActive = false; // 取消激活！触发 OnDeactivated，自动取消 messenger 订阅
        base.OnNavigatedFrom(e);

        _speechRecognizer.Dispose();

    }

    private async void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (sender.Text is { Length: > 0 } text)
        {
            //查询单词
            _timerQueue.Stop();
            Progress.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            Progress.ShowPaused = false;
            Progress.ShowError = false;
            var dbService = App.GetService<EnglishWordContext>();
            var result = await dbService.QueryWordAsync(text);
            if (result is not null)
            {
                ViewModel.CurrentWord = ViewModel.MakeWordModel(result);
                Progress.ShowError = false;
            }
            else
            {
                //todo: 提示
                ViewModel.CurrentWord = ViewModel.MakeWordModel(null);
                Progress.ShowError = true;
            }
            Progress.ShowPaused = true;

            _timerQueue.Debounce(() =>
            {
                Progress.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }, TimeSpan.FromSeconds(5));

            var likeWordEntrys = await dbService.Match(text);
            var modelList = likeWordEntrys
            .Select(e => ViewModel.MakeWordModel(e))
            .OrderByDescending(e => e.Collins)
            .ThenByDescending(e => e.Bnc)
            .ThenByDescending(e => e.Frq)
            .ToList();

            ViewModel.Items = new ObservableCollection<WordModel>(modelList);
        }

    }

    private void OnItemGridViewItemClick(object sender, ItemClickEventArgs e)
    {

    }

    private void ShadowRect_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        //shadow.Receivers.Add(ContentArea);
    }

    public async void Receive(ShowWordDetailDialog message)
    {
        // 1. Measure：传入可用空间，无限大可用 new Size(double.PositiveInfinity, double.PositiveInfinity)
        StandardPopupContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // DesiredSize 就是它想要的尺寸
        Size desired = StandardPopupContent.DesiredSize;
        var needW = desired.Width;
        var needH = desired.Height;

        var w = (ContentArea.ActualWidth - needW) / 2;
        var h = (ContentArea.ActualHeight - needH) / 2;
        StandardPopup.HorizontalOffset = w;
        StandardPopup.VerticalOffset = h;
        StandardPopup.IsOpen = true;
    }

    public async void Receive(ShowSpeechRecognizer message)
    {
        ViewModel.SpeechIcon = "\uE7C8";
        ViewModel.isSpeeching = true;

        _speechRecognizer.UIOptions.ExampleText = message.word.Word;
        _speechRecognizer.Constraints.Clear();
        _speechRecognizer.Constraints.Add(new SpeechRecognitionListConstraint([message.word.Word])); // 词表约束，纯本地
        SpeechRecognitionCompilationResult result1 = await _speechRecognizer.CompileConstraintsAsync();

        if (result1.Status != SpeechRecognitionResultStatus.Success)
        {
            // 处理语法编译失败（如语言包未安装）
            return;
        }
        var result2 = await _speechRecognizer.RecognizeWithUIAsync();
        if (result2.Status == SpeechRecognitionResultStatus.Success)
        {
            var text = result2.Text;
        }
        //todo  对 错

        ViewModel.SpeechIcon = "\uE720";
        ViewModel.isSpeeching = false;
    }
}
