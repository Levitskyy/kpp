using UnityEngine;

/// <summary>
/// One decode + jitter-buffer pipeline for a single speaker. A VoiceEndpoint
/// holds one VoiceDecoder per currently-active speaker feeding into it - a
/// player's own mouth normally has exactly one, a radio can have several
/// simultaneously (each teammate transmitting gets their own decoder, mixed
/// together by the endpoint on playback).
/// </summary>
public class VoiceDecoder
{
    private readonly IVoiceCodec _codec;
    private readonly VoiceRingBuffer _ringBuffer;
    private readonly byte[] _decodeBuffer = new byte[1024 * 50];
    private float[] _floatScratch = new float[2048];

    /// <summary>Timestamp of the last packet received - used by the owning
    /// endpoint to garbage-collect decoders for speakers who went silent.</summary>
    public float LastPacketTime { get; private set; }

    public VoiceDecoder(IVoiceCodec codec, int targetLatencyMs, int maxLatencyMs)
    {
        _codec = codec;
        _ringBuffer = new VoiceRingBuffer((int)codec.SampleRate, targetLatencyMs, maxLatencyMs);
        LastPacketTime = Time.realtimeSinceStartup;
    }

    /// <summary>Call from the main thread (inside the RPC callback) when a network packet arrives.</summary>
    public void PushPacket(byte[] packet, int length)
    {
        if (!_codec.Decode(packet, length, _decodeBuffer, out uint bytesWritten) || bytesWritten == 0)
            return;

        int sampleCount = (int)bytesWritten / 2;
        if (_floatScratch.Length < sampleCount)
            _floatScratch = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short s = (short)(_decodeBuffer[i * 2] | (_decodeBuffer[i * 2 + 1] << 8));
            _floatScratch[i] = s / 32768f;
        }

        _ringBuffer.Write(_floatScratch, sampleCount);
        LastPacketTime = Time.realtimeSinceStartup;
    }

    /// <summary>Call from the audio thread to pull this speaker's contribution into the mix.</summary>
    public void Read(float[] destination, int count) => _ringBuffer.Read(destination, count);
}