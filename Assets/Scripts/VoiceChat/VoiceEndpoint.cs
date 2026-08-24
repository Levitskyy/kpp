using System.Collections.Generic;
using PurrNet;
using UnityEngine;

/// <summary>
/// A place in the world voice is heard from - a player's own mouth
/// (proximity chat, normally one speaker) or a radio (potentially several
/// teammates transmitting at once). Holds one VoiceDecoder per currently
/// active speaker, mixes all of them together each audio buffer, then runs
/// the combined signal through this endpoint's effect chain (radio crackle,
/// room reverb, distance muffling) and manual panning.
///
/// Manual panning/attenuation instead of spatialBlend, because Unity's
/// built-in 3D panner doesn't apply to audio generated via OnAudioFilterRead
/// (see AudioSource.spatialBlend = 0f below).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceEndpoint : MonoBehaviour
{
    [Header("Latency")]
    [SerializeField] private int targetLatencyMs = 60;
    [SerializeField] private int maxLatencyMs = 200;

    [Header("Spatialization")]
    [Tooltip("Off for things like a radio that should sound the same regardless of position.")]
    [SerializeField] private bool use3DPositioning = true;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private float muffledCutoffHz = 800f;
    [SerializeField] private float clearCutoffHz = 8000f;

    [Header("Cleanup")]
    [Tooltip("Remove a speaker's decoder after this many seconds of silence, so it doesn't sit around forever.")]
    [SerializeField] private float speakerTimeoutSeconds = 2f;

    private AudioSource _audioSource;
    private readonly Dictionary<PlayerID, VoiceDecoder> _decoders = new Dictionary<PlayerID, VoiceDecoder>();
    private readonly object _decodersLock = new object();
    private List<PlayerID> _idleScratchList; // reused to avoid per-frame allocation

    private float[] _mixScratch = new float[2048];
    private float[] _perSpeakerScratch = new float[2048];
    private int _sampleRate = 48000; // updated once we see the first codec used here

    private readonly LowPassVoiceEffect _distanceMuffle = new LowPassVoiceEffect();
    private readonly List<IVoiceEffect> _effects = new List<IVoiceEffect>();

    private Transform _listenerTransform;
    private float _pan;
    private float _gain = 1f;

    public void AddEffect(IVoiceEffect effect) => _effects.Add(effect);
    public void RemoveEffect(IVoiceEffect effect) => _effects.Remove(effect);

    /// <summary>Clears every effect's internal state (delay lines, filter memory) - e.g. on teleport.</summary>
    public void ResetEffects()
    {
        _distanceMuffle.Reset();
        for (int i = 0; i < _effects.Count; i++)
            (_effects[i] as VoiceEffectBase)?.Reset();
    }

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.clip = null;
        _audioSource.loop = true;

        _audioSource.Play();
    }

    private void Start()
    {
        var gm = InstanceHandler.GetInstance<GameManager>();
        gm.OnAudioListenerSpawned += OnListenerSpawned;
        if (gm.GetAudioListener())
        {
            _listenerTransform = gm.GetAudioListener().transform;
        }
    }


    private void OnDestroy()
    {
        var gm = InstanceHandler.GetInstance<GameManager>();
        gm.OnAudioListenerSpawned -= OnListenerSpawned;
    }

    private void Update()
    {
        if (use3DPositioning && _listenerTransform != null)
        {
            Vector3 toSource = transform.position - _listenerTransform.position;
            float distance = toSource.magnitude;
            Vector3 localDir = _listenerTransform.InverseTransformDirection(toSource.normalized);

            _pan = Mathf.Clamp(localDir.x, -1f, 1f);
            _gain = 1f - Mathf.Clamp01((distance - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));
            _distanceMuffle.CutoffHz = Mathf.Lerp(muffledCutoffHz, clearCutoffHz, _gain);
        }
        else
        {
            _pan = 0f;
            _gain = 1f;
        }
        CleanupIdleSpeakers();

        Debug.Log($"{_gain} :: {gameObject}");
    }

    /// <summary>
    /// Route a network voice packet from `speaker` into this endpoint.
    /// Safe to call for several different speakers concurrently - each one
    /// gets its own VoiceDecoder automatically, mixed together on playback.
    /// </summary>
    public void PushVoicePacket(PlayerID speaker, IVoiceCodec codec, byte[] packet, int length)
    {
        VoiceDecoder decoder;
        lock (_decodersLock)
        {
            if (!_decoders.TryGetValue(speaker, out decoder))
            {
                decoder = new VoiceDecoder(codec, targetLatencyMs, maxLatencyMs);
                _decoders[speaker] = decoder;
                _sampleRate = (int)codec.SampleRate;
            }
        }
        decoder.PushPacket(packet, length);
    }

    private void CleanupIdleSpeakers()
    {
        lock (_decodersLock)
        {
            if (_decoders.Count == 0) return;

            float now = Time.realtimeSinceStartup;
            _idleScratchList ??= new List<PlayerID>();
            _idleScratchList.Clear();

            foreach (var kv in _decoders)
                if (now - kv.Value.LastPacketTime > speakerTimeoutSeconds)
                    _idleScratchList.Add(kv.Key);

            for (int i = 0; i < _idleScratchList.Count; i++)
                _decoders.Remove(_idleScratchList[i]);
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        int frameCount = data.Length / channels;
        EnsureScratchSize(frameCount);
        System.Array.Clear(_mixScratch, 0, frameCount);

        lock (_decodersLock)
        {
            foreach (var kv in _decoders)
            {
                kv.Value.Read(_perSpeakerScratch, frameCount);
                for (int i = 0; i < frameCount; i++)
                    _mixScratch[i] += _perSpeakerScratch[i];
            }
        }

        // Effects run once on the combined signal
        _distanceMuffle.Process(_mixScratch, frameCount, _sampleRate);
        for (int e = 0; e < _effects.Count; e++)
            _effects[e].Process(_mixScratch, frameCount, _sampleRate);

        float panAngle = (_pan + 1f) * Mathf.PI / 4f;
        float leftGain = Mathf.Cos(panAngle) * _gain;
        float rightGain = Mathf.Sin(panAngle) * _gain;

        for (int i = 0; i < frameCount; i++)
        {
            float sample = _mixScratch[i];
            if (channels == 2)
            {
                data[i * 2] = sample * leftGain;
                data[i * 2 + 1] = sample * rightGain;
            }
            else
            {
                for (int c = 0; c < channels; c++)
                    data[i * channels + c] = sample * _gain;
            }
        }
    }

    private void EnsureScratchSize(int frameCount)
    {
        if (_mixScratch.Length < frameCount) _mixScratch = new float[frameCount];
        if (_perSpeakerScratch.Length < frameCount) _perSpeakerScratch = new float[frameCount];
    }

    private void OnListenerSpawned(AudioListener listener)
    {
        _listenerTransform = listener.transform;
    }
}