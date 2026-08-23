public interface IVoiceEffect
{
    /// <summary>Mutates `samples` in place. `count` samples, mono, at `sampleRate` Hz.</summary>
    void Process(float[] samples, int count, int sampleRate);
}