using System;
using System.ComponentModel;
using Game.Scripts.Common.Network.FacepunchTransport;
using PurrNet;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public sealed class SteamConnectionManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private uint appid = 480;
    private Lobby? _currentLobby;

    public Lobby? CurrentLobby => _currentLobby;
    public bool IsHost => _currentLobby.HasValue && _currentLobby.Value.Owner.Id == SteamClient.SteamId;

    public static event Action OnHostStarted;
    public static event Action OnClientStartred;

    private void Awake()
    {
        InitSteam();
        InstanceHandler.RegisterInstance(this);
    } 
    private void InitSteam()
    {
        try
        {
            SteamClient.Init(appid);
            Debug.Log($"init as steam: {SteamClient.Name}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Couldn't initialize steam: {e}");
        }
    }

    private void OnEnable()
    {
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
    }

    private void OnDisable()
    {
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
    }

    private void OnDestroy()
    {
        InstanceHandler.UnregisterInstance<SteamConnectionManager>();
    }


    // -------------------------- HOSTING GAME --------------------------------
    // <summary>Call this when you want to host game</summary>
    public async void HostGame()
    {
        if (!SteamClient.IsValid)
        {
            // TO REDO (NOT JUST LOGERROR)
            Debug.LogError("Steam is not initialized!");
            return;
        }

        var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        if (!lobby.HasValue)
        {
            // TO REDO (NOT JUST LOGERROR)
            Debug.LogError("Couldn't create steam lobby!");
        }

        _currentLobby = lobby.Value;
        _currentLobby.Value.SetFriendsOnly();
        _currentLobby.Value.SetJoinable(true);
        _currentLobby.Value.SetData("game", "KPP");
    
        StartPurrNetHost();
    }

    private void StartPurrNetHost()
    {
        var transport = NetworkManager.main.transport as FacepunchTransport;
        if (transport == null)
        {
            Debug.LogError("No FacepunchTransport on NetworkManager");
            return;
        }

        transport.Initialize(SteamClient.SteamId.ToString(), 0, null);
        NetworkManager.main.StartHost();
        NetworkManager.main.sceneModule.LoadSceneAsync("KPP");
        OnHostStarted?.Invoke();
    }

    // <summary>Opens steam invite overlay to current lobby</summary>
    public void OpenInviteOverlay()
    {
        if (_currentLobby.HasValue)
        {
            SteamFriends.OpenGameInviteOverlay(_currentLobby.Value.Id);
        }
    }

    // -----------------------CLIENT-------------------
    private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId id)
    {
        var enterResult = await lobby.Join();
        if (enterResult != RoomEnter.Success)
        {
            // TO REDO (NOT JUST LOG)
            Debug.LogWarning($"Couldn't connect to lobby: {enterResult}");
        }
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        _currentLobby = lobby;

        SteamFriends.SetRichPresence("status", "Заступает в наряд");
        SteamFriends.SetRichPresence("connect", _currentLobby.Value.Id.ToString());

        if (lobby.Owner.Id == SteamClient.SteamId)
        {
            return; // we are the host;
        }

        StartPurrNetClient(lobby.Owner.Id);
    }

    private void StartPurrNetClient(SteamId hostId)
    {
        var transport = NetworkManager.main.transport as FacepunchTransport;
        if (transport == null)
        {
            Debug.LogError("No FacepunchTransport on NetworkManager");
            return;
        }

        transport.Initialize(hostId.ToString(), 0, null);
        NetworkManager.main.StartClient();
        OnClientStartred?.Invoke();
    }

    public void LeaveLobby()
    {
        if (!_currentLobby.HasValue) return;

        bool wasHost = IsHost;
        _currentLobby.Value.Leave();
        _currentLobby = null;

        if (wasHost)
        {
            NetworkManager.main.StopServer();
        }
        else
        {
            NetworkManager.main.StopClient();
        }

        SteamFriends.ClearRichPresence();
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
        SteamClient.Shutdown();        
    }
}