using UnityEngine;

public sealed class PersistentRoot : MonoBehaviour
{
    private static PersistentRoot _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}