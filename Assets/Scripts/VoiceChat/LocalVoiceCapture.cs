using System;
using PurrNet;
using UnityEngine;

public class LocalVoiceCapture : MonoBehaviour
{
    public event Action<byte[], int> PacketCaptured;

    private IVoiceSource _voiceSource;
    private bool _isActive;

    public uint SampleRate => _voiceSource?.SampleRate ?? 48000;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
        Activate();
    }

    /// <summary>Call once (from PlayerVoice.OnSpawned, only if isOwner). Idempotent.</summary>
    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
 
        _voiceSource = new MicrophoneVoiceSource();
        _voiceSource.StartCapture();
    }
 
    private void Update()
    {
        if (!_isActive) return;
 
        if (_voiceSource.TryGetPacket(out var data, out var length))
            PacketCaptured?.Invoke(data, length);
    }
 
    private void OnDestroy()
    {
        if (_isActive) _voiceSource.StopCapture();
        InstanceHandler.UnregisterInstance<LocalVoiceCapture>();
    }
}