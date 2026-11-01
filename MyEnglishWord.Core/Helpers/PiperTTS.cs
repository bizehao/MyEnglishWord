using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MyEnglishWord.Core.Helpers;

public static unsafe partial class PiperTTS
{

    public const int PIPER_OK = 0;
    public const int PIPER_DONE = 1;

    public partial struct piper_synthesizer
    {
    }

    public unsafe partial struct piper_audio_chunk
    {
        public float* samples;

        public nuint num_samples;

        public int sample_rate;

        public byte is_last;

        public uint* phonemes;

        public nuint num_phonemes;

        public int* phoneme_ids;

        public nuint num_phoneme_ids;

        public int* alignments;

        public nuint num_alignments;
    }

    public partial struct piper_synthesize_options
    {
        public int speaker_id;

        public float length_scale;

        public float noise_scale;

        public float noise_w_scale;
    }

    public unsafe partial struct piper_create_options
    {
        public nuint struct_size;

        public sbyte* model_path;

        public sbyte* config_path;

        public sbyte* espeak_data_path;

        public sbyte* g2pw_model_dir;

        public sbyte* data_dir;
    }


    public static void piper_init_create_options(piper_create_options* opts)
    {
        if ((opts) != null)
        {
            opts->struct_size = (nuint)sizeof(piper_create_options);
            opts->model_path = null;
            opts->config_path = null;
            opts->espeak_data_path = null;
            opts->g2pw_model_dir = null;
            opts->data_dir = null;
        }
    }

    [LibraryImport("piper")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial piper_synthesizer* piper_create_with_options(piper_create_options* options);

    [LibraryImport("piper", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial piper_synthesizer* piper_create(string model_path, string? config_path, string? espeak_data_path);

    [LibraryImport("piper")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void piper_free(piper_synthesizer* synth);

    [LibraryImport("piper")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial piper_synthesize_options piper_default_synthesize_options(piper_synthesizer* synth);

    [LibraryImport("piper", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int piper_synthesize_start(piper_synthesizer* synth, string text, piper_synthesize_options* options);

    [LibraryImport("piper")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int piper_synthesize_next(piper_synthesizer* synth, piper_audio_chunk* chunk);

    [LibraryImport("piper")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial sbyte* piper_version();

}



