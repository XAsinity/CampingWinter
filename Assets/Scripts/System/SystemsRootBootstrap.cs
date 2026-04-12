using UnityEngine;

public class SystemsRootBootstrap : MonoBehaviour
{
    static SystemsRootBootstrap _instance;

    [Header("Auto Systems")]
    [SerializeField] bool ensurePlayerSettings = true;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (ensurePlayerSettings && GetComponent<PlayerSettings>() == null)
            gameObject.AddComponent<PlayerSettings>();
    }
}
