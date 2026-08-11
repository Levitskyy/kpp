using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Interactor : NetworkBehaviour
{
    [SerializeField] private float rayDistance = 1.5f;
    [SerializeField] private GameObject camera;
    private InputAction action;
    private InteractionUI ui;


    protected override void OnSpawned()
    {
        enabled = isOwner;

        if (!isOwner) return;
        ui = InstanceHandler.GetInstance<InteractionUI>();
        action = InputSystem.actions.FindAction("Interact");
    }
    private void Update()
    {
        if (!isOwner) return;
        CheckHit();
    }
    private void CheckHit()
    {
        RaycastHit hit;

        Debug.DrawRay(camera.transform.position, camera.transform.forward * rayDistance, Color.red);
        var layerMask = LayerMask.GetMask("Interact");
        if (Physics.Raycast(
            camera.transform.position, 
            camera.transform.forward, 
            out hit, 
            rayDistance, 
            layerMask
            ))
        {
            Interactable intr = hit.transform.GetComponent<Interactable>();
            ui.ShowText(intr.GetName());

            if (action.WasPressedThisFrame())
            {
                // TO BE REFACTORED IF NOT ONLY ITEM USERS COULD USE INTERACTABLES  !!!!!
                // ALSO REFACTOR INTERACTABLE Interact FUNCTION !!!!!!!!!!!!!!!!!!!!!!!!!
                intr.Interact(GetComponent<ItemUser>());
            }
        }
        else
        {
            ui.Clear();
        }
    }
}
