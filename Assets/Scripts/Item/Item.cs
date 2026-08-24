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

        locationState.onChanged += OnLocationStateChanged;
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.isKinematic = !isController;
        networkTransform = GetComponent<NetworkTransform>();
        OnLocationStateChanged(locationState.value);
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();

        locationState.onChanged -= OnLocationStateChanged;
    }

    protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
    {
        base.OnOwnerChanged(oldOwner, newOwner, asServer);
        if (newOwner.HasValue)
        {
            networkTransform.StartIgnoringParentChanges();
            networkTransform.enabled = false;
        }
        else
        {
            networkTransform.StopIgnoringParentChanges();
            networkTransform.enabled = true;
        }
    }

    public override void Interact(ItemUser user)
    {
        user.TryPickupItem(this);
    }

    [ServerRpc(runLocally: true)]
    public void SetItemState(ItemLocationState newLocationState)
    {
        if (newLocationState.Location != ItemLocation.World)
        {
            GiveOwnership(newLocationState.Holder.owner, propagateToChildren: true);
        } 
        else
        {
            GiveOwnership(null, propagateToChildren: true); 
        }
        
        locationState.value = newLocationState;
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

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        networkTransform.StartIgnoringParentChanges();
        networkTransform.enabled = false;

        var slot = state.Holder.GetItemSlotTransform();
        transform.SetParent(slot, false);
        transform.localPosition = GetDeltaPosition();
        transform.localRotation = GetRotation();
    }

    public void ApplyInventory()
    {
        if (col) col.enabled = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        networkTransform.StartIgnoringParentChanges();
        networkTransform.enabled = false;
        gameObject.SetActive(false);

        transform.SetParent(null);
    }

    public void ApplyWorld()
    {
        gameObject.SetActive(true);
        if (col) col.enabled = true;

        transform.SetParent(null, true);
        var landedPos = transform.position;
        var landedRot = transform.rotation;

        rb.isKinematic = !isServer;

        networkTransform.enabled = true;
        networkTransform.ClearInterpolation(landedPos, landedRot, transform.localScale);
        networkTransform.StopIgnoringParentChanges();
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
