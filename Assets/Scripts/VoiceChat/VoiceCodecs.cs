public class PassthroughVoiceCodec : IVoiceCodec
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