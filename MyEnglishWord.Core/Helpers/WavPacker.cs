using System;
using System.Collections.Generic;
using System.Text;

namespace MyEnglishWord.Core.Helpers;

public static class WavPacker
{
    /// <summary>
    /// float32 PCM 打包成 WAV（IEEE float）
    /// </summary>
    /// <param name="samples">float采样，交错：L R L R 立体声</param>
    /// <param name="sampleRate">44100 / 48000</param>
    /// <param name="channels">1单声道，2立体声</param>
    /// <returns>完整wav二进制</returns>
    public static byte[] FloatPcmToWav(byte[] samples, int sampleRate)
    {
        int channels = 1;
        int bitsPerSample = 32;
        int blockAlign = channels * bitsPerSample / 8;
        int byteRate = sampleRate * blockAlign;
        int dataSize = samples.Length;
        int fileSize = 4 + (8 + 16) + (8 + dataSize);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8);

        // RIFF
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(fileSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // subchunk1 size
        bw.Write((ushort)3); // 3 = IEEE float
        bw.Write((ushort)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((ushort)blockAlign);
        bw.Write((ushort)bitsPerSample);

        // data
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        // data write
        bw.Write(samples);

        bw.Flush();
        return ms.ToArray();
    }
}
