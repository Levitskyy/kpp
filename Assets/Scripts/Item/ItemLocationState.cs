using System;

public enum ItemLocation
{
    World,
    Held,
    Inventory
}

public readonly struct ItemLocationState
{
    public ItemLocation Location { get; }
    public ItemUser Holder { get; }

    private ItemLocationState(ItemLocation location, ItemUser holder)
    {
        Location = location;
        Holder = holder;
    }

    public static ItemLocationState World()
    {
        return new(ItemLocation.World, null);
    }

    public static ItemLocationState Held(ItemUser holder)
    {
        if (holder == null)
            throw new ArgumentNullException(nameof(holder));

        return new(ItemLocation.Held, holder);
    }

    public static ItemLocationState Inventory(ItemUser holder)
    {
        if (holder == null)
            throw new ArgumentNullException(nameof(holder));

        return new(ItemLocation.Inventory, holder);
    }
}