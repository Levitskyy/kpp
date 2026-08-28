using PurrNet;
using UnityEngine;

public enum ItemAction
{
    PrimaryPress,
    PrimaryRelease,
    SecondaryPress,
    SecondaryRelease,

}

public enum ItemState
{
    World,
    Held,
    Inventory,
}
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public abstract class Item : Interactable
{
    private SyncVar<ItemLocationState> locationState = new(initialValue: ItemLocationState.World());
    [SerializeField] protected Vector3 deltaPos;
    [SerializeField] protected Quaternion rot = Quaternion.identity;
    private Rigidbody rb;
    private Collider col;
    private NetworkTransform networkTransform;

    public Quaternion GetRotation() => rot;
    public Vector3 GetDeltaPosition() => deltaPos;
    public abstract void HandleAction(ItemAction action, ItemContext ctx);

    protected override void OnSpawned()
    {
        base.OnSpawned();
        if (isServer)
        {
            GiveOwnership(networkManager.localPlayer, propagateToChildren: true);
        }

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.isKinematic = !isController;
        networkTransform = GetComponent<NetworkTransform>();
        
        locationState.onChanged += OnLocationStateChanged;
        OnLocationStateChanged(locationState.value);
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();

        locationState.onChanged -= OnLocationStateChanged;
    }

    public override void Interact(ItemUser user)
    {
        user.TryPickupItem(this);
    }

    

    [ServerRpc(requireOwnership: false, runLocally: true)]
    public void SetItemState(ItemLocationState newLocationState)
    {

        if (!isServer && newLocationState.Location != ItemLocation.Held) OnLocationStateChanged(newLocationState);

        if (isServer)
        {
            if (newLocationState.Location != ItemLocation.World)
            {
                GiveOwnership(newLocationState.Holder.owner, propagateToChildren: true);
            }

            locationState.value = newLocationState;
        }
    }

    private void OnLocationStateChanged(ItemLocationState newLocationState)
    {
        switch (newLocationState.Location)
        {
            case ItemLocation.Held:
                ApplyHeld(newLocationState);
                break;

            case ItemLocation.Inventory:
                ApplyInventory();
                break;

            case ItemLocation.World:
                ApplyWorld();
                break;
        }
    }

    public void ApplyHeld(ItemLocationState state)
    {
        gameObject.SetActive(true);
        if (col) col.enabled = false;

        rb.isKinematic = true;

        var slot = state.Holder.GetItemSlotTransform();
        transform.SetParent(slot, false);
        transform.localPosition = GetDeltaPosition();
        transform.localRotation = GetRotation();
    }

    public void ApplyInventory()
    {
        if (col) col.enabled = false;

        rb.isKinematic = true;
        gameObject.SetActive(false);
        transform.SetParent(null);
    }

    public void ApplyWorld()
    {
        gameObject.SetActive(true);
        if (col) col.enabled = true;

        transform.SetParent(null, true);

        rb.isKinematic = !isController;
    }

    private void Update()
    {
        //Debug.Log(locationState.value.Location);
    }
}

public struct ItemContext
{
    public ItemUser user;
}
