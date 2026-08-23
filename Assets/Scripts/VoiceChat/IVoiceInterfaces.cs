public interface IVoiceCodec
{
    /// <summary>Sample rate of the PCM this codec produces.</summary>
    uint SampleRate { get; }
 
    /// <summary>
    /// Decodes a network packet into 16-bit PCM bytes (little-endian, mono).
    /// Returns false if the packet was invalid/unusable.
    /// </summary>
    bool Decode(byte[] input, int inputLength, byte[] outputPcm, out uint outputBytesWritten);
}

public interface IVoiceSource
{
    uint SampleRate { get; }
    void StartCapture();
    void StopCapture();
 
    /// <summary>
    /// Returns true if there's a new packet ready to send, false otherwise.
    /// Call this once per tick
    /// </summary>
    bool TryGetPacket(out byte[] data, out int length);
}