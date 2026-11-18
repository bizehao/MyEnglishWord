using System.Collections.ObjectModel;
using System.Diagnostics;
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
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;
using MyEnglishWord.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Media.Capture;
using Windows.Media.SpeechRecognition;
using WinRT;

namespace MyEnglishWord.Views;

public sealed partial class MainPage : Page
    , IRecipient<ShowWordDetailDialog>
    , IRecipient<ShowSpeechRecognizer>
{
    //private delegate void PageClosed();

    public MainViewModel ViewModel
    {
        get;
    }

    private readonly DispatcherQueueTimer _timerQueueOfProgress;
    private readonly DispatcherQueueTimer _timerQueueOfInfoBar;

    private bool _isRecognitionTaskRunning = false;

    private Func<Task>? _stopRecognition;



    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        WeakReferenceMessenger.Default.RegisterAll(this);

        _timerQueueOfProgress = DispatcherQueue.CreateTimer();
        _timerQueueOfInfoBar = DispatcherQueue.CreateTimer();

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
        ViewModel.IsActive = true; // 激活，自动注册 Messenger IRecipient

        if (e.Parameter is string {Length: > 0 } word)
        {
            SearchBox.Text = word;
            AutoSuggestBox_QuerySubmitted(SearchBox, new AutoSuggestBoxQuerySubmittedEventArgs());
        }
    }

    protected async override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.IsActive = false; // 取消激活！触发 OnDeactivated，自动取消 messenger 订阅

        if (_stopRecognition is not null)
        {
            await _stopRecognition();
        }

        base.OnNavigatedFrom(e);
    }

    private async void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (sender.Text is { Length: > 0 } text)
        {
            //查询单词
            _timerQueueOfProgress.Stop();
            Progress.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            Progress.ShowPaused = false;
            Progress.ShowError = false;
            var dbService = App.GetService<EnglishWordContext>();
            var result = await dbService.QueryWordAsync(text);

            if (result is not null)
            {
                var myWordRecordContext = App.GetService<MyWordRecordContext>();
                var hasWord = await myWordRecordContext.Has(text);
                var wd = WordModel.MakeWordModel(result);
                wd.InDictionary = hasWord;
                ViewModel.CurrentWord = wd;
                Progress.ShowError = false;
            }
            else
            {
                //todo: 提示
                ViewModel.CurrentWord = WordModel.Entry;
                Progress.ShowError = true;
            }
            Progress.ShowPaused = true;

            _timerQueueOfProgress.Debounce(() =>
            {
                Progress.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }, TimeSpan.FromSeconds(5));

            var likeWordEntrys = await dbService.Match(text);
            var modelList = likeWordEntrys
            .Select(e => WordModel.MakeWordModel(e))
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
        //StandardPopupContent.Measure(new Size(715, double.PositiveInfinity));
        //Size desired = StandardPopupContent.DesiredSize;
        //var needW = desired.Width;
        //var needH = (desired.Height + 4);

        //TestData.Text = needW.ToString() + " - " + needH.ToString();

        //if (needH > ContentArea.ActualHeight * 5 / 7)
        //{
        //    needH = ContentArea.ActualHeight * 5 / 7;
        //}

        //var w = (ContentArea.ActualWidth - needW) / 2;
        //var h = (ContentArea.ActualHeight - needH) / 2;

        //StandardPopupScrollView.ScrollTo(0, 0);
        //StandardPopupGrid.Height = needH;

        //StandardPopup.HorizontalOffset = w;
        //StandardPopup.VerticalOffset = h;
        StandardPopupGrid.Width = StandardPopupPos.ActualWidth;
        StandardPopupGrid.Height = StandardPopupPos.ActualHeight;
        StandardPopup.IsOpen = true;

    }

    public async void Receive(ShowSpeechRecognizer message)
    {
        //todo: 保持页面单例
        if (_isRecognitionTaskRunning)
        {
            return;
        }

        _isRecognitionTaskRunning = true;

        //先关闭infobar 提示
        _timerQueueOfInfoBar.Stop();
        ViewModel.SpeekInfoBarOpen = false;

        //todo 限制只能一个识别
        if (ViewModel.IsSpeeching)
        {
            ViewModel.IsSpeeching = false;
            if (_stopRecognition is not null)
            {
                await _stopRecognition();
            }
        }
        else
        {

            var hasPer = await AudioCapturePermissions.RequestMicrophonePermission();
            if (!hasPer)
            {
                return;
            }

            ViewModel.IsSpeeching = true;

            var language = new Language("en-US");
            using var speechRecognizer = new SpeechRecognizer(language);
            speechRecognizer.UIOptions.AudiblePrompt = "please speak";
            speechRecognizer.UIOptions.ShowConfirmation = true;
            speechRecognizer.UIOptions.ExampleText = message.word.Word;
            speechRecognizer.Constraints.Add(new SpeechRecognitionListConstraint([message.word.Word])); // 词表约束，纯本地

            SpeechRecognitionCompilationResult compileResult = await speechRecognizer.CompileConstraintsAsync();
            if (compileResult.Status != SpeechRecognitionResultStatus.Success)
            {
                SpeekInfoBarOP("CompileConstraints Error, 请检查系统语音包是否安装", InfoBarSeverity.Error);
                return;
            }

            var stopRecognize = async () =>
            {
                await speechRecognizer.StopRecognitionAsync();
            };

            _stopRecognition = stopRecognize;

            var textResult = await speechRecognizer.RecognizeAsync();

            _stopRecognition = null;

            if (!ViewModel.IsSpeeching)
            {
                return;
            }

            if (textResult.Status == SpeechRecognitionResultStatus.Success)
            {
                var text = textResult.Text;
                if (text == message.word.Word)
                {
                    SpeekInfoBarOP("读音正确", InfoBarSeverity.Success);
                }
                else
                {
                    SpeekInfoBarOP("读音错误", InfoBarSeverity.Error);
                }
            }
            else
            {
                //todo:显示错
                SpeekInfoBarOP("录音发生错误", InfoBarSeverity.Error);
            }
            ViewModel.IsSpeeching = false;
        }

        _isRecognitionTaskRunning = false;
    }

    private void SpeekInfoBarOP(string msg, InfoBarSeverity infoBarSeverity, bool open = true)
    {
        _timerQueueOfInfoBar.Stop();

        if (open)
        {
            ViewModel.SpeekInfoBarOpen = true;
            ViewModel.SpeekInfoBarMsg = msg;
            ViewModel.SpeekInfoBarSeverity = infoBarSeverity;

            _timerQueueOfInfoBar.Debounce(() =>
            {
                ViewModel.SpeekInfoBarOpen = false;
            }, TimeSpan.FromSeconds(3));
        }
        else
        {
            ViewModel.SpeekInfoBarOpen = false;
        }
    }

    private void ClosePopupButton_Click(object sender, RoutedEventArgs e)
    {
        StandardPopup.IsOpen = false;
    }
}
