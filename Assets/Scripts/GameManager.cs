using System;
using PurrNet;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public event Action<AudioListener> OnAudioListenerSpawned;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    private void OnDestroy()
    {
        InstanceHandler.UnregisterInstance<GameManager>();
    }

    public void InvokeAudioListenerSpawned(AudioListener listener)
    {
        OnAudioListenerSpawned?.Invoke(listener);
    }
}