using System;
using System.Collections.Generic;
using System.Linq;
using PurrNet;
using UnityEngine;

public class Inventory : NetworkBehaviour
{
    [SerializeField] private int capacity = 4;
    [SerializeField] private static int initCapacity = 4; 
    private SyncArray<Item> items = new(length: initCapacity, ownerAuth: false);

    [HideInInspector] public int Capacity => capacity;
    public int Count => items.Count(x => x);

    public Item Get(int id) => id >= 0 && id < Capacity ? items[id] : null;

    [ServerRpc]
    public void TryAddItem(Item item, ItemUser user, int slotNumber)
    {
        if (Count >= Capacity) return;
        if (slotNumber >= Capacity)
        {
            throw new ArgumentOutOfRangeException("slot number out of inventory capacity");
        }

        items[slotNumber] = item;
        item.SetItemState(ItemLocationState.Held(user));
    }

    [ServerRpc]
    public void RemoveItem(int slotNumber)
    {

        var item = Get(slotNumber);
        if (!item) return;

        item.SetItemState(ItemLocationState.World());
        items[slotNumber] = null;
    }

}
