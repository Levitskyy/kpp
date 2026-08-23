using UnityEngine;

/// <summary>
/// Simple one-pole low-pass filter. Cuts high frequencies - use for muffling
/// distant voices, or as one half of a radio bandpass effect.
/// CutoffHz can be changed at runtime (e.g. from Update(), based on distance) -
/// it's just a plain field, cheap to update every frame.
/// </summary>
public class LowPassVoiceEffect : VoiceEffectBase
{
    public float CutoffHz = 3000f;
    private float _prevSample;

    public override void Process(float[] samples, int count, int sampleRate)
    {
        float rc = 1f / (2f * Mathf.PI * CutoffHz);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);

        for (int i = 0; i < count; i++)
        {
            _prevSample += alpha * (samples[i] - _prevSample);
            samples[i] = _prevSample;
        }
    }

    public override void Reset() => _prevSample = 0f;
}

/// <summary>
/// Simple one-pole high-pass filter. Cuts low frequencies - combine with
/// LowPassVoiceEffect to get a bandpass, which is most of what makes voice
/// sound like it's coming through a small radio speaker.
/// </summary>
public class HighPassVoiceEffect : VoiceEffectBase
{
    public float CutoffHz = 400f;
    private float _prevInput;
    private float _prevOutput;

    public override void Process(float[] samples, int count, int sampleRate)
    {
        float rc = 1f / (2f * Mathf.PI * CutoffHz);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);

        for (int i = 0; i < count; i++)
        {
            float input = samples[i];
            float output = alpha * (_prevOutput + input - _prevInput);
            _prevInput = input;
            _prevOutput = output;
            samples[i] = output;
        }
    }

    public override void Reset() => _prevInput = _prevOutput = 0f;
}

/// <summary>
/// Composite "bad radio" effect: bandpass filter (walkie-talkie speakers
/// have almost no bass or treble) + soft clipping distortion (radios
/// compress/clip hard) + occasional static crackle.
/// </summary>
public class RadioVoiceEffect : VoiceEffectBase
{
    private readonly LowPassVoiceEffect _lowPass = new LowPassVoiceEffect { CutoffHz = 2900f };
    private readonly HighPassVoiceEffect _highPass = new HighPassVoiceEffect { CutoffHz = 900f };

    [Range(0f, 1f)] public float DistortionAmount = 1.0f;
    [Range(0f, 0.05f)] public float CrackleAmount = 0f;

    private readonly System.Random _rng = new System.Random();

    public override void Process(float[] samples, int count, int sampleRate)
    {
        _highPass.Process(samples, count, sampleRate);
        _lowPass.Process(samples, count, sampleRate);

        for (int i = 0; i < count; i++)
        {
            float s = samples[i];
            s = (float)System.Math.Tanh(s * (1f + DistortionAmount * 4f));

            if (_rng.NextDouble() < CrackleAmount)
                s += (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.4f;

            samples[i] = Mathf.Clamp(s, -1f, 1f);
        }
    }

    public override void Reset()
    {
        _lowPass.Reset();
        _highPass.Reset();
    }
}

/// <summary>
/// Simple feedback delay line for an echo effect.
/// </summary>
public class EchoVoiceEffect : VoiceEffectBase
{
    public float DelaySeconds = 0.18f;
    [Range(0f, 0.95f)] public float Feedback = 0.35f;
    [Range(0f, 1f)] public float Mix = 0.3f;

    private readonly float[] _delayBuffer;
    private int _writePos;

    public EchoVoiceEffect(int sampleRate, float maxDelaySeconds = 0.5f)
    {
        _delayBuffer = new float[Mathf.CeilToInt(sampleRate * maxDelaySeconds)];
    }

    public override void Process(float[] samples, int count, int sampleRate)
    {
        int delaySamples = Mathf.Clamp(
            Mathf.RoundToInt(DelaySeconds * sampleRate), 1, _delayBuffer.Length - 1);

        for (int i = 0; i < count; i++)
        {
            int readPos = (_writePos - delaySamples + _delayBuffer.Length) % _delayBuffer.Length;
            float delayed = _delayBuffer[readPos];

            float input = samples[i];
            _delayBuffer[_writePos] = input + delayed * Feedback;
            _writePos = (_writePos + 1) % _delayBuffer.Length;

            samples[i] = input * (1f - Mix) + delayed * Mix;
        }
    }

    public override void Reset()
    {
        System.Array.Clear(_delayBuffer, 0, _delayBuffer.Length);
        _writePos = 0;
    }
}


public abstract class VoiceEffectBase : IVoiceEffect
{
    public abstract void Process(float[] samples, int count, int sampleRate);
 
    /// <summary>Clear any internal buffers/filter memory. Default: no-op.</summary>
    public virtual void Reset() { }
}