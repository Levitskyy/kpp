using System;
using Steamworks;
using UnityEngine;

public sealed class SteamConnectionManager : MonoBehaviour
{
    private uint appid = 480;

    private void Awake()
    {
        InitSteam();

        DontDestroyOnLoad(gameObject);
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
}