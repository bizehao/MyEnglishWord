using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MyEnglishWord.Helpers;
using MyEnglishWord.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MyEnglishWord.Views;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MiniMainPage : Page
         , IRecipient<ShowCurrentWord>
         , IRecipient<ShowSpeechRecognizer>
{

    private readonly DispatcherQueueTimer _timerQueueOfInfoBar;

    private Func<Task>? _stopRecognition;

    public MiniMainViewModel ViewModel
    {
        get;
    }
    public MiniMainPage()
    {
        ViewModel = new MiniMainViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        _timerQueueOfInfoBar = DispatcherQueue.CreateTimer();
    }

    public async void Receive(ShowCurrentWord message)
    {
        ViewModel.CurrentWord = message.Word;
        await MiniMainViewModel.AddExtentFromWordNet(ViewModel.CurrentWord);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        WeakReferenceMessenger.Default.Register((IRecipient<ShowSpeechRecognizer>)this, MiniMainViewModel.Token);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    public async void Receive(ShowSpeechRecognizer message)
    {
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

            _stopRecognition = async () =>
            {
                await speechRecognizer.StopRecognitionAsync();
            };

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
}
