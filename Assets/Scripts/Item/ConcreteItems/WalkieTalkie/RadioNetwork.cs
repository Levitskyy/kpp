using PurrNet;
using PurrNet.Transports;

public class RadioNetwork : NetworkBehaviour
{
    private SyncList<WalkieTalkie> _walkieTalkies = new();
    
    [ServerRpc(requireOwnership: false)]
    public void AddWalkieTalkie(WalkieTalkie value)
    {
        _walkieTalkies.Add(value);
    }

    [ServerRpc(requireOwnership: false)]
    public void RemoveWalkieTalkie(WalkieTalkie value)
    {
        _walkieTalkies.Remove(value);
    }

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    protected override void OnDespawned()
    {
        InstanceHandler.UnregisterInstance<RadioNetwork>();
    }

    [ServerRpc(Channel.Unreliable, requireOwnership: false)]
    public void SendPacket(WalkieTalkie source, byte[] data)
    {
        TransmitToEachOther(source, data);
    }

    [ObserversRpc(Channel.Unreliable)]
    private void TransmitToEachOther(WalkieTalkie source, byte[] data)
    {
        foreach (var walkieTalkie in _walkieTalkies)
        {
            if (walkieTalkie == source) continue;
            walkieTalkie.AcceptTransmittedPacket(source.owner.Value, source.GetVoiceCodec(), data);
        }
    }
}