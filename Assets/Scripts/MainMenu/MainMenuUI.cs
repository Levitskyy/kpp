using PurrNet;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button _createGameButton;

    private void Awake()
    {
        _createGameButton.onClick.AddListener(OnCreateGamePressed);
    }

    private void OnCreateGamePressed()
    {
        InstanceHandler.GetInstance<SteamConnectionManager>().HostGame();
    }
}
