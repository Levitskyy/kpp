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
public abstract class Item : Interactable
{
    private SyncVar<ItemLocationState> locationState = new(initialValue: ItemLocationState.World());
    [SerializeField] protected Vector3 deltaPos;
    [SerializeField] protected Quaternion rot = Quaternion.identity;
    private Rigidbody rb;

    public Quaternion GetRotation() => rot;
    public Vector3 GetDeltaPosition() => deltaPos;
    public abstract void HandleAction(ItemAction action, ItemContext ctx);

    protected override void OnSpawned()
    {
        base.OnSpawned();

        locationState.onChanged += OnLocationStateChanged;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !isController;
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

    [ServerRpc]
    public void SetItemState(ItemLocationState newLocationState)
    {
        locationState.value = newLocationState;
        if (newLocationState.Location != ItemLocation.World)
        {
            GiveOwnership(newLocationState.Holder.owner, propagateToChildren: true);
        } 
        else
        {
            GiveOwnership(null, propagateToChildren: true); 
        }
    }

    private void OnLocationStateChanged(ItemLocationState newLocationState)
    {
        var col = GetComponent<Collider>();
        if (newLocationState.Location == ItemLocation.Held)
        {
            gameObject.SetActive(true);
            if (col) col.enabled = false;
            rb.isKinematic = true;
            
            transform.position = newLocationState.Holder.GetItemSlotTransform().position;
            transform.SetParent(newLocationState.Holder.GetItemSlotTransform(), true);
            transform.localPosition += GetDeltaPosition();
            transform.rotation = Quaternion.identity;
            transform.localRotation = GetRotation(); 
        }
        else if (newLocationState.Location == ItemLocation.Inventory)
        {
             if (col) col.enabled = false;
            rb.isKinematic = true;
            gameObject.SetActive(false);
        }
        else if (newLocationState.Location == ItemLocation.World)
        {
            gameObject.SetActive(true);
            if (col) col.enabled = true;
            rb.isKinematic = !isServer;
            transform.SetParent(null);
        }
    }
}

public struct ItemContext
{
    public ItemUser user;
}
