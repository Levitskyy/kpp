using System;
using System.IO;
using Steamworks;
using UnityEngine;

/// <summary>
/// Captures raw (uncompressed) PCM from the system microphone using Unity's
/// built-in Microphone API. No compression - this is for testing the full
/// pipeline
/// </summary>
public sealed class MicrophoneVoiceSource : IVoiceSource
{
    public uint SampleRate { get; }

    private const int ChunkMilliseconds = 20;
    private readonly int _samplesPerChunk;

    private AudioClip _micClip;
    private string _micDevice;
    private int _lastReadPos;
    private readonly float[] _floatScratch;
    private readonly byte[] _byteScratch;

    public MicrophoneVoiceSource(uint sampleRate = 48000)
    {
        SampleRate = sampleRate;
        _samplesPerChunk = (int)(sampleRate * ChunkMilliseconds / 1000);
        _floatScratch = new float[_samplesPerChunk];
        _byteScratch = new byte[_samplesPerChunk * 2]; // 16-bit PCM
    }

    public void StartCapture()
    {
        _micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (_micDevice == null)
        {
            Debug.LogWarning("No microphone device found.");
            return;
        }

        
        _micClip = Microphone.Start(_micDevice, true, 1, (int)SampleRate);
        _lastReadPos = 0;
    }

    public void StopCapture()
    {
        if (_micDevice != null)
            Microphone.End(_micDevice);
    }

    public bool TryGetPacket(out byte[] data, out int length)
    {
        data = null;
        length = 0;

        if (_micClip == null)
            return false;

        int currentPos = Microphone.GetPosition(_micDevice);
        int available = currentPos - _lastReadPos;
        if (available < 0) available += _micClip.samples; // clip wrapped around

        if (available < _samplesPerChunk)
            return false; // not enough new audio yet for a full chunk

        _micClip.GetData(_floatScratch, _lastReadPos);
        _lastReadPos = (_lastReadPos + _samplesPerChunk) % _micClip.samples;

        // Convert float [-1,1] -> 16-bit PCM bytes (like steam)
        for (int i = 0; i < _samplesPerChunk; i++)
        {
            short s = (short)(Mathf.Clamp(_floatScratch[i], -1f, 1f) * 32767f);
            _byteScratch[i * 2] = (byte)(s & 0xFF);
            _byteScratch[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }

        data = _byteScratch;
        length = _byteScratch.Length;
        return true;
    }
}

public sealed class SteamVoiceSource : IVoiceSource
    {
        public uint SampleRate { get; }
 
        private bool _isCapturing;
 
        // Переиспользуемый буфер захвата, чтобы не аллоцировать MemoryStream
        // каждый кадр. ВАЖНО: возвращаемый TryGetPacket массив — это тот же
        // внутренний буфер этого стрима, переиспользуемый на следующий кадр.
        // Если потребитель не отправляет пакет немедленно в том же кадре
        // (как это делает LocalVoiceCapture.Update -> PacketCaptured), а
        // хранит ссылку дольше — нужно скопировать данные самостоятельно.
        private readonly MemoryStream _captureStream = new MemoryStream(4096);

        public SteamVoiceSource(uint sampleRate = 48000)
        {
            SampleRate = sampleRate == 0 ? SteamUser.OptimalSampleRate : sampleRate;
        }
 
        public void StartCapture()
        {
            if (_isCapturing) return;
 
            SteamUser.VoiceRecord = true;
            _isCapturing = true;
        }
 
        public void StopCapture()
        {
            if (!_isCapturing) return;
 
            SteamUser.VoiceRecord = false;
            _isCapturing = false;
        }
 
        public bool TryGetPacket(out byte[] data, out int length)
        {
            data = null;
            length = 0;
 
            if (!_isCapturing)
                return false;
 
            if (!SteamUser.HasVoiceData)
                return false;
 
            _captureStream.Position = 0;
            _captureStream.SetLength(0);
 
            int written;
            try
            {
                written = SteamUser.ReadVoiceData(_captureStream);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamVoiceSource] ReadVoiceData failed: {e.Message}");
                return false;
            }
 
            if (written <= 0)
                return false;
 
            data = _captureStream.GetBuffer(); // валидны только первые `length` байт
            length = written;
            return true;
        }
    }