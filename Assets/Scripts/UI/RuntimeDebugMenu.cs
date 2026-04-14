using UnityEngine;
using UnityEngine.InputSystem;

public class RuntimeDebugMenu : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug Menu")]
    [SerializeField] Key toggleMenuKey = Key.F10;
    [SerializeField] bool startVisible;

    bool _visible;
    bool _ignorePlayerDeath;
    bool _infiniteFlashlightBattery;
    bool _hideMainMenuUi;
    string _status;

    void Start()
    {
        _visible = startVisible;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[toggleMenuKey].wasPressedThisFrame)
            _visible = !_visible;

        ApplyDebugToggles();
    }

    void OnGUI()
    {
        if (!_visible)
            return;

        const float width = 360f;
        GUILayout.BeginArea(new Rect(16f, 16f, width, 280f), GUI.skin.box);
        GUILayout.Label("Runtime Debug Menu");
        GUILayout.Space(8f);

        _ignorePlayerDeath = GUILayout.Toggle(_ignorePlayerDeath, "Ignore Player Death");
        _infiniteFlashlightBattery = GUILayout.Toggle(_infiniteFlashlightBattery, "Infinite Flashlight Battery");
        _hideMainMenuUi = GUILayout.Toggle(_hideMainMenuUi, "Hide Main Menu UI (Temp)");

        GUILayout.Space(10f);
        if (GUILayout.Button("Reset Save + Settings (One Click)", GUILayout.Height(30f)))
            ResetAllPersistentData();

        if (!string.IsNullOrWhiteSpace(_status))
        {
            GUILayout.Space(10f);
            GUILayout.Label(_status);
        }

        GUILayout.EndArea();
    }

    void ApplyDebugToggles()
    {
        NightSystem nightSystem = NightSystem.Instance != null ? NightSystem.Instance : FindFirstObjectByType<NightSystem>();
        if (nightSystem != null)
            nightSystem.SetDebugIgnorePlayerDeath(_ignorePlayerDeath);

        PlayerFlashlight flashlight = FindFirstObjectByType<PlayerFlashlight>();
        if (flashlight != null)
            flashlight.SetDebugInfiniteBattery(_infiniteFlashlightBattery);

        TemporaryMainMenu menu = FindFirstObjectByType<TemporaryMainMenu>();
        if (menu != null)
            menu.SetMenuUiHiddenByDebug(_hideMainMenuUi);
        else
            _hideMainMenuUi = false;
    }

    void ResetAllPersistentData()
    {
        NightSystem nightSystem = NightSystem.Instance != null ? NightSystem.Instance : FindFirstObjectByType<NightSystem>();
        if (nightSystem != null)
            nightSystem.ResetAllSaveData();

        PlayerSettings settings = PlayerSettings.Instance != null ? PlayerSettings.Instance : FindFirstObjectByType<PlayerSettings>();
        if (settings != null)
        {
            settings.DeleteSavedSettingsFile();
            settings.ReloadFromDiskAndApply();
            settings.ApplyAllSettings(saveToDisk: true);
        }

        _status = "Persistent save/settings reset complete.";
    }
#endif
}
