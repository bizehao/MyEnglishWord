using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using MyEnglishWord.Core.Helpers;
using MyEnglishWord.ViewModels;
using NAudio.MediaFoundation;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using WinRT;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static CommunityToolkit.Mvvm.ComponentModel.__Internals.__TaskExtensions.TaskAwaitableWithoutEndValidation;

namespace MyEnglishWord.Views;

public sealed partial class 空白2Page : Page
{
    public 空白2ViewModel ViewModel
    {
        get;
    }

    public 空白2Page()
    {
        ViewModel = App.GetService<空白2ViewModel>();
        InitializeComponent();
    }

    private unsafe PiperTTS.piper_synthesizer* _synth;
    private PiperTTS.piper_synthesize_options _options;

    private bool _is_init = false;
    private readonly MediaPlayer _player = new ();

    private unsafe Task Init()
    {
        return Task.Run(() =>
        {
            //var ss = PiperTTS.piper_version();
            //var version = NativeHelper.Utf8ByteToString((byte*)ss);
            //int a = 0;
            var onnx_path = "C:/Users/bzh/Downloads/tts_model/en_US-lessac-high.onnx";
            var json_path = "C:/Users/bzh/Downloads/tts_model/en_US-lessac-high.onnx.json";
            var espeak_data_dir = "C:/Users/bzh/Downloads/tts_model/espeak-ng-data";
            _synth = PiperTTS.piper_create(onnx_path, json_path, espeak_data_dir);
            _options = PiperTTS.piper_default_synthesize_options(_synth);
        });

        //piper_free(synth);
    }

    private Task<byte[]> getWordWav(string word)
    {
        return Task.Run(()=>{
            byte[] wav = [];
            using var stream = new MemoryStream();
            unsafe
            {
                int code;
                fixed (PiperTTS.piper_synthesize_options* options_ptr = &_options)
                {
                    code = PiperTTS.piper_synthesize_start(_synth, word, options_ptr);
                }
                    
                if (code == PiperTTS.PIPER_OK)
                {
                    PiperTTS.piper_audio_chunk chunk;
                    do
                    {
                        code = PiperTTS.piper_synthesize_next(_synth, &chunk);
                        if (code != PiperTTS.PIPER_DONE)
                        {
                            throw new Exception("piper piper_synthesize_next error");
                        }
                        var byteSize = chunk.num_samples * sizeof(float);
                        var result = new byte[byteSize];
                        fixed (byte* dst = result)
                        {
                            System.Buffer.MemoryCopy(chunk.samples, dst, byteSize, byteSize);
                        }
                        stream.Write(result);
                    }
                    while (chunk.is_last != PiperTTS.PIPER_DONE);

                    wav = WavPacker.FloatPcmToWav(stream.ToArray(), chunk.sample_rate);
                }
            }
            return wav;
        });
    }

    private async void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!_is_init)
        {
            await Init();
            _is_init = true;
        }

        //var wordWav = await getWordWav("hello world");
        //File.WriteAllBytes(@"C:\Users\bzh\Downloads\tts_model\test.wav", wordWav);

        


        var wordWav = await getWordWav("hello world");

        var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        writer.WriteBytes(wordWav);

        await writer.StoreAsync();

        var mediaSource = MediaSource.CreateFromStream(stream, "audio/wav");
        
        _player.Source = mediaSource;
        _player.Play();

    }
}
