using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(VoiceEndpoint))]
public class PlayerVoice : NetworkBehaviour
{
    private LocalVoiceCapture _voiceSource;   // only exists on the owner (captures mic)
    private IVoiceCodec _voiceCodec;     // exists on every client (needed to decode)
    private VoiceEndpoint _endpoint;     // this player's own "mouth"
    private InputAction _voiceAction;
    private bool _wantsToSpeak;

    protected override void OnSpawned()
    {
        // REDO TO GET SAMLERATE FROM VOICE SOURCE
        base.OnSpawned();
        _endpoint = GetComponent<VoiceEndpoint>();
        _endpoint.enabled = !isOwner;
        _voiceSource = InstanceHandler.GetInstance<LocalVoiceCapture>();
        if (_voiceSource.IsUsingSteamVoice)
        {
            _voiceCodec = new SteamVoiceCodec(_voiceSource.SampleRate);
        }
        else
        {
            _voiceCodec = new PassthroughVoiceCodec(_voiceSource.SampleRate);
        }

        if (!isOwner) return;
        _voiceSource.Activate();
        _voiceSource.PacketCaptured += OnPacketCaptured;
        _voiceAction = InputSystem.actions.FindAction("Voice");
        
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        if (!isOwner) return;

        _voiceSource.PacketCaptured -= OnPacketCaptured;
    }

    private void OnPacketCaptured(byte[] data, int length)
    {
        if (!_wantsToSpeak) return;

        var trimmed = new byte[length];
        System.Buffer.BlockCopy(data, 0, trimmed, 0, length);
        SendVoicePacket(trimmed);
    }

    private void Update()
    {
        if (!isOwner) return;

        if (_voiceAction.WasPressedThisFrame())
        {
            _wantsToSpeak = true;
        }
        if (_voiceAction.WasReleasedThisFrame())
        {
            _wantsToSpeak = false;
        }
    }

    [ServerRpc(Channel.Unreliable)]
    private void SendVoicePacket(byte[] packet)
    {
        RelayVoicePacket(packet);
    }

    // Proximity chat: the speaker is always this object's own owner, so we
    // don't need RPCInfo here the way the radio does - ownership already
    // guarantees only this player could have sent it.
    [ObserversRpc(Channel.Unreliable, excludeOwner: true, runLocally: false)]
    private void RelayVoicePacket(byte[] packet)
    {
        _endpoint.PushVoicePacket(owner.Value, _voiceCodec, packet, packet.Length);
    }
}