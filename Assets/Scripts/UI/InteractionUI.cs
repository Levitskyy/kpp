using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// REFACTOR CAUSE ITS CALLED EVERY FRAME
public class InteractionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text interactionText;
    private InputAction interactionAction;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
        interactionText.enabled = false;
        interactionAction = InputSystem.actions.FindAction("Interact");
    }

    private void OnDestroy()
    {
        InstanceHandler.UnregisterInstance<InteractionUI>();        
    }
    public void ShowText(string text)
    {
        string binding = interactionAction.GetBindingDisplayString();
        binding = binding.Split()[1];
        string outText = $"[{binding}] {text}";
        interactionText.enabled = true;
        interactionText.text = outText;
    }

    public void Clear()
    {
        interactionText.enabled = false;
    }
}
