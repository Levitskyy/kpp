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
    private NetworkTransform networkTransform;

    public Quaternion GetRotation() => rot;
    public Vector3 GetDeltaPosition() => deltaPos;
    public abstract void HandleAction(ItemAction action, ItemContext ctx);

    protected override void OnSpawned()
    {
        base.OnSpawned();

        locationState.onChanged += OnLocationStateChanged;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !isController;
        networkTransform = GetComponent<NetworkTransform>();
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
            networkTransform.enabled = false; // it anyway gets it transform from parent
        }
        else if (newLocationState.Location == ItemLocation.Inventory)
        {
             if (col) col.enabled = false;
            rb.isKinematic = true;
            gameObject.SetActive(false);
            networkTransform.enabled = false;
        }
        else if (newLocationState.Location == ItemLocation.World)
        {
            gameObject.SetActive(true);
            networkTransform.enabled = true;
            if (col) col.enabled = true;
            rb.isKinematic = !isServer;
            transform.SetParent(null);
        }
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
