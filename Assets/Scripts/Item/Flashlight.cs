using PurrNet;
using Unity.VisualScripting;
using UnityEngine;

public class Flashlight : Item
{
    private SyncVar<bool> _state = new (false, ownerAuth:true);
    [SerializeField] private Light light;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        _state.onChanged += OnStateChanged;
        light.enabled = _state.value;

    }
    public override void HandleAction(ItemAction action, ItemContext ctx)
    {
        switch (action)
        {
            case ItemAction.PrimaryPress:
                Toggle();
                break;
            case ItemAction.PrimaryRelease:
                break;
            case ItemAction.SecondaryPress:
                break;
            case ItemAction.SecondaryRelease:
                break;
            default:
                return;
        };
    }

    private void OnStateChanged(bool newVal)
    {
        light.enabled = newVal;
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        _state.onChanged -= OnStateChanged;
    }

    private void SetEnabled(bool enabled)
    {
        _state.value = enabled;
    }
    private void Toggle()
    {
        SetEnabled(!_state.value);
    }

}
