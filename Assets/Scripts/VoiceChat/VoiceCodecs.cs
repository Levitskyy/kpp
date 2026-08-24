using System;
using System.IO;
using Steamworks;
using UnityEngine;

public sealed class PassthroughVoiceCodec : IVoiceCodec
{
    public uint SampleRate { get; }
 
    public PassthroughVoiceCodec(uint sampleRate)
    {
        SampleRate = sampleRate;
    }
 
    public bool Decode(byte[] input, int inputLength, byte[] outputPcm, out uint outputBytesWritten)
    {
        System.Array.Copy(input, outputPcm, inputLength);
        outputBytesWritten = (uint)inputLength;
        return true;
    }
}

public sealed class SteamVoiceCodec : IVoiceCodec
    {
        public uint SampleRate { get; }
 
        // Расширяемый переиспользуемый буфер декодирования (НЕ обёрнут вокруг
        // outputPcm — тот может быть меньше, чем реально декодирует Steam).
        private readonly MemoryStream _decodeStream = new MemoryStream(4096);
 
        public SteamVoiceCodec(uint sampleRate = 0)
        {
            SampleRate = sampleRate > 0 ? sampleRate : SteamUser.OptimalSampleRate;
        }
 
        public bool Decode(byte[] input, int inputLength, byte[] outputPcm, out uint outputBytesWritten)
        {
            outputBytesWritten = 0;
 
            if (input == null || inputLength <= 0)
                return false;
 
            if (outputPcm == null || outputPcm.Length == 0)
                return false;
 
            byte[] compressed = input;
            if (inputLength != input.Length)
            {
                compressed = new byte[inputLength];
                Buffer.BlockCopy(input, 0, compressed, 0, inputLength);
            }
 
            // SampleRate у SteamUser — глобальное состояние, не параметр вызова.
            // Выставляем перед каждым декодом на случай, если что-то ещё в игре
            // (например, другой SteamVoiceCodec с другой частотой) его меняло.
            SteamUser.SampleRate = SampleRate;
 
            _decodeStream.Position = 0;
            _decodeStream.SetLength(0);
 
            int written;
            try
            {
                // Пишем в расширяемый стрим — Steam сам решает, сколько PCM
                // байт получится на выходе, мы это не контролируем заранее.
                written = SteamUser.DecompressVoice(compressed, _decodeStream);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamVoiceCodec] Decode failed: {e.Message}");
                return false;
            }
 
            if (written <= 0)
                return false;
 
            int toCopy = Math.Min(written, outputPcm.Length);
            if (toCopy < written)
            {
                Debug.LogWarning(
                    $"[SteamVoiceCodec] Decoded {written} bytes but outputPcm is only " +
                    $"{outputPcm.Length} bytes — truncating. Увеличь размер буфера в VoiceDecoder.");
            }
 
            Buffer.BlockCopy(_decodeStream.GetBuffer(), 0, outputPcm, 0, toCopy);
            outputBytesWritten = (uint)toCopy;
            return true;
        }
    }
