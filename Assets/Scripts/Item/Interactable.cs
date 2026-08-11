using PurrNet;
using UnityEngine;

public abstract class Interactable : NetworkBehaviour
{
    [SerializeField] protected string _name;
    public string GetName() => _name;
    public abstract void Interact(ItemUser user);
}