using System;
using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Inventory))]
public class ItemUser : NetworkBehaviour
{
    private int activeSlot = 0;
    private Inventory inventory;
    private Item currentItem => inventory.Get(activeSlot);
    [SerializeField] private Transform itemPos;
    public Transform GetItemSlotTransform() => itemPos;

    private InputAction useItemPrimaryAction;
    private InputAction useItemSecondaryAction;
    private InputAction dropItemAction;
    private InputAction nextActiveSlotAction;
    private InputAction prevActiveSlotAction;

    protected override void OnSpawned()
    {
        if (!isOwner) return;

        inventory = GetComponent<Inventory>();

        useItemPrimaryAction = InputSystem.actions.FindAction("UseItemPrimary");
        useItemSecondaryAction = InputSystem.actions.FindAction("UseItemSecondary");
        dropItemAction = InputSystem.actions.FindAction("DropItem");
        nextActiveSlotAction = InputSystem.actions.FindAction("NextActiveSlot");
        prevActiveSlotAction = InputSystem.actions.FindAction("PrevActiveSlot");
    }

    private void Update()
    {
        if (isOwner)
        {
            CheckForInputs();
        }
    }
    private void HandleActionCurrentItem(ItemAction action)
    {
        if (currentItem == null) return;

        ItemContext ctx = new ItemContext{user = this};
        currentItem.HandleAction(action, ctx);
    }

    public void TryPickupItem(Item item)
    {
        if (inventory.Count >= inventory.Capacity) return;

        inventory.TryAddItem(item, this, activeSlot);
    }

    private void ChangeActiveSlot(int id)
    {
        if (id < 0 || id >= inventory.Capacity)
        {
            throw new ArgumentOutOfRangeException("invalid active slot index");
        }

        if (currentItem)
        {
            currentItem.SetItemState(ItemLocationState.Inventory(this));
        }

        activeSlot = id;
        if (currentItem)
        {
            currentItem.SetItemState(ItemLocationState.Held(this));
        }
    }

    private void NextSlot()
    {
        Debug.Log("next slot");
        ChangeActiveSlot((activeSlot + 1) % inventory.Capacity);
    }

    private void PrevSlot()
    {
         Debug.Log("prev slot");
        if (activeSlot == 0)
        {
         ChangeActiveSlot(inventory.Capacity - 1);
         return;
        }

        ChangeActiveSlot((activeSlot - 1) % inventory.Capacity);
    }

    private void CheckForInputs()
    {
        if (useItemPrimaryAction.WasPressedThisFrame())
        {
            HandleActionCurrentItem(ItemAction.PrimaryPress);
        }
        if (useItemPrimaryAction.WasReleasedThisFrame())
        {
            HandleActionCurrentItem(ItemAction.PrimaryRelease);
        }
        if (useItemSecondaryAction.WasPressedThisFrame())
        {
            HandleActionCurrentItem(ItemAction.SecondaryPress);
        }
        if (useItemSecondaryAction.WasReleasedThisFrame())
        {
            HandleActionCurrentItem(ItemAction.SecondaryRelease);
        }
        if (dropItemAction.WasPressedThisFrame())
        {
            inventory.RemoveItem(activeSlot);
        }
        if(nextActiveSlotAction.ReadValue<float>() > 0)
        {
            NextSlot();
        }
        if (prevActiveSlotAction.ReadValue<float>() > 0)
        {
            PrevSlot();
        }
    }
}