using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using Microsoft.UI.Xaml.Shapes;
using MyEnglishWord.Core.Helpers;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace MyEnglishWord.Helpers;

public class PlayVoice : IDisposable
{
    public enum VoiceType
    {
        Microsoft,
        Piper
    }

    private record class SelectVoice(string VoiceName, VoiceType VoiceType);

    private readonly struct AutoReleaserSemaphoreSlim : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public AutoReleaserSemaphoreSlim(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
            _semaphore.Wait();
        }

        public void Dispose() => _semaphore.Release();
    }

    // 复用MediaPlayer，避免多次创建
    private readonly MediaPlayer _player = new();

    private SelectVoice _selectVoice = new("Piper Lessac", VoiceType.Piper);
    private SelectVoice? _reset_selectVoice;

    //play type 1
    private SpeechSynthesizer? _microsoftSynth;

    //play type 2
    private unsafe PiperTTS.piper_synthesizer* _piperSynth;
    private PiperTTS.piper_synthesize_options _options;

    //--------------------------------------------------------//

    private readonly SemaphoreSlim _gate = new(1, 1);

    public PlayVoice()
    {

    }

    public void Reset(string voice, VoiceType type)
    {
        _reset_selectVoice = new(voice, type);
    }

    public async Task Init()
    {
        bool has_piperSynth;
        bool has_microsoftSynth;
        bool op;

        unsafe
        {
            has_microsoftSynth = _microsoftSynth != null;
            has_piperSynth = _piperSynth != null;
            op = _selectVoice != _reset_selectVoice || (!has_microsoftSynth && !has_piperSynth);
        }

        if (op)
        {
            _selectVoice = _reset_selectVoice!;

            await Task.Run(() =>
            {
                using var arss = new AutoReleaserSemaphoreSlim(_gate);
                
                if (_selectVoice.VoiceType == VoiceType.Microsoft)
                {
                    var voice = SpeechSynthesizer.AllVoices.First(e => e.DisplayName == _selectVoice.VoiceName);
                    if (has_microsoftSynth)
                    {
                        _microsoftSynth!.Voice = voice;
                    }
                    else
                    {
                        _microsoftSynth = new SpeechSynthesizer()
                        {
                            Voice = voice
                        };
                    }
                    releasePiper();
                }
                else
                {
                    if (has_piperSynth)
                    {
                        return;
                    }
                    var onnx_path = "C:/Users/bzh/Other/model/en_US-lessac-high.onnx";
                    var json_path = "C:/Users/bzh/Other/model/en_US-lessac-high.onnx.json";
                    var espeak_data_dir = "C:/Users/bzh/Other/model/espeak-ng-data";
                    unsafe
                    {
                        _piperSynth = PiperTTS.piper_create(onnx_path, json_path, espeak_data_dir);
                        _options = PiperTTS.piper_default_synthesize_options(_piperSynth);
                    }
                    releaserMicrosoft();
                }
            
            });
        }
    }

    public async Task Play(string word)
    {
        await _gate.WaitAsync();

        //synth.Options.SpeakingRate = 1.0;

        _player.Source = await makeVoiceSource(word);
        _player.Play();

        _gate.Release();

    }

    public void Dispose()
    {
        _player.Dispose();
        releaserMicrosoft();
        releasePiper();
    }

    private void releaserMicrosoft()
    {
        _microsoftSynth?.Dispose();
        _microsoftSynth = null;
    }
    private void releasePiper()
    {
        unsafe
        {
            if (_piperSynth != null)
            {
                PiperTTS.piper_free(_piperSynth);
                _piperSynth = null;
            }
        }
    }

    private async Task<IMediaPlaybackSource> makeVoiceSource(string word)
    {
        if (_selectVoice.VoiceType == VoiceType.Microsoft)
        {
            var stream = await _microsoftSynth!.SynthesizeTextToStreamAsync(word);
            var source = MediaSource.CreateFromStream(stream, stream.ContentType);
            return new MediaPlaybackItem(source);
        }
        else
        {
            var wordWav = await Task.Run(() =>
            {
                byte[] wav = [];
                using var stream = new MemoryStream();
                unsafe
                {
                    int code;
                    fixed (PiperTTS.piper_synthesize_options* options_ptr = &_options)
                    {
                        code = PiperTTS.piper_synthesize_start(_piperSynth, word, options_ptr);
                    }

                    if (code == PiperTTS.PIPER_OK)
                    {
                        PiperTTS.piper_audio_chunk chunk;
                        do
                        {
                            code = PiperTTS.piper_synthesize_next(_piperSynth, &chunk);
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

            var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(wordWav);

            await writer.StoreAsync();

            return MediaSource.CreateFromStream(stream, "audio/wav");
        }

    }
}
