using PurrNet;
using UnityEngine;

[RequireComponent(typeof(VoiceEndpoint))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class WalkieTalkie : Item
{
    private VoiceEndpoint _voiceEndpoint;
    private LocalVoiceCapture _localVoice;
    private IVoiceCodec _voiceCodec;
    public IVoiceCodec GetVoiceCodec() => _voiceCodec;

    private bool _wantsToSpeak = false;


    public override void HandleAction(ItemAction action, ItemContext ctx)
    {
        switch(action)
        {
            case ItemAction.PrimaryPress:
                _wantsToSpeak = true;
                break;
            case ItemAction.PrimaryRelease:
                _wantsToSpeak = false;
                break;
        }
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        _localVoice = InstanceHandler.GetInstance<LocalVoiceCapture>();
        _voiceCodec = new PassthroughVoiceCodec(_localVoice.SampleRate);
        _voiceEndpoint = GetComponent<VoiceEndpoint>();
        _voiceEndpoint.AddEffect(new RadioVoiceEffect());
        _localVoice.PacketCaptured += OnPacketCaptured;    

        if (isServer)
        {
            InstanceHandler.GetInstance<RadioNetwork>().AddWalkieTalkie(this);
        }
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();

        _localVoice.PacketCaptured -= OnPacketCaptured;

        if (isServer)
        {
            InstanceHandler.GetInstance<RadioNetwork>().RemoveWalkieTalkie(this);
        }
    }

    private void OnPacketCaptured(byte[] data, int length)
    {
        if (!_wantsToSpeak) return;

        var trimmed = new byte[length];
        System.Buffer.BlockCopy(data, 0, trimmed, 0, length);
        InstanceHandler.GetInstance<RadioNetwork>().SendPacket(this, trimmed);
    }

    public void AcceptTransmittedPacket(PlayerID speaker, IVoiceCodec codec, byte[] data)
    {
        _voiceEndpoint.PushVoicePacket(speaker, codec, data, data.Length);
    }
}