using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using EngineShadowQuality = UnityEngine.ShadowQuality;

public class PlayerSettings : MonoBehaviour
{
    [Serializable]
    class PlayerSettingsData
    {
        public bool enableVSync;
        public int targetFrameRate;
        public int qualityLevel;
        public int shadowQuality;
        public bool enableMotionBlur;
        public float masterVolume;
        public float menuMusicVolume;
        public float mouseSensitivity;
    }

    [Header("Performance")]
    [SerializeField] bool enableVSync;
    [SerializeField, Min(30)] int targetFrameRate = 60;

    [Header("Quality")]
    [SerializeField] int qualityLevel = 2;
    [SerializeField] EngineShadowQuality shadowQuality = EngineShadowQuality.All;
    [SerializeField] bool enableMotionBlur = true;

    [Header("Sound")]
    [SerializeField, Range(0f, 4f)] float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] float menuMusicVolume = 0.75f;

    [Header("Controls")]
    [SerializeField, Range(0.2f, 8f)] float mouseSensitivity = 2f;

    [Header("Persistence")]
    [SerializeField] bool persistAcrossScenes = true;
    [SerializeField] bool loadSavedSettingsOnStart = true;
    [SerializeField] bool saveOnApplicationPause = true;

    string _settingsFilePath;

    public static PlayerSettings Instance { get; private set; }

    public bool EnableVSync
    {
        get => enableVSync;
        set => enableVSync = value;
    }

    public int TargetFrameRate
    {
        get => targetFrameRate;
        set => targetFrameRate = Mathf.Max(30, value);
    }

    public int QualityLevelIndex
    {
        get => qualityLevel;
        set => qualityLevel = Mathf.Max(0, value);
    }

    public EngineShadowQuality ShadowQualitySetting
    {
        get => shadowQuality;
        set => shadowQuality = value;
    }

    public bool EnableMotionBlur
    {
        get => enableMotionBlur;
        set => enableMotionBlur = value;
    }

    public float MasterVolume
    {
        get => masterVolume;
        set => masterVolume = Mathf.Clamp(value, 0f, 4f);
    }

    public float MenuMusicVolume
    {
        get => menuMusicVolume;
        set => menuMusicVolume = Mathf.Clamp01(value);
    }

    public float MouseSensitivity
    {
        get => mouseSensitivity;
        set => mouseSensitivity = Mathf.Clamp(value, 0.2f, 8f);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        _settingsFilePath = Path.Combine(Application.persistentDataPath, "player-settings.json");

        LoadSettingsFromDisk();
    }

    void Start()
    {
        if (Instance != this)
            return;

        ApplyAllSettings(saveToDisk: false);
    }

    void OnEnable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnValidate()
    {
        if (Instance != this)
            return;

        if (!Application.isPlaying)
            return;

        ApplyAllSettings();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (Instance != this)
            return;

        if (pauseStatus && saveOnApplicationPause)
            SaveSettingsToDisk();
    }

    void OnApplicationQuit()
    {
        if (Instance != this)
            return;

        SaveSettingsToDisk();
    }

    public void ApplyAllSettings(bool saveToDisk = true)
    {
        ApplyPerformanceSettings();
        ApplyQualitySettings();
        ApplyVisualSettings();
        ApplySoundSettings();
        ApplyControlSettings();

        if (saveToDisk)
            SaveSettingsToDisk();
    }

    public void ApplyAndSave()
    {
        ApplyAllSettings(saveToDisk: true);
    }

    public void ReloadFromDiskAndApply()
    {
        LoadSettingsFromDisk();
        ApplyAllSettings(saveToDisk: false);
    }

    void ApplyPerformanceSettings()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = enableVSync ? 1 : 0;

        if (!enableVSync)
            Application.targetFrameRate = Mathf.Max(30, targetFrameRate);
    }

    void ApplyQualitySettings()
    {
        int maxQualityIndex = Mathf.Max(0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(Mathf.Clamp(qualityLevel, 0, maxQualityIndex), true);
        QualitySettings.shadows = shadowQuality;
    }

    void ApplyVisualSettings()
    {
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || volume.profile == null)
                continue;

            if (volume.profile.TryGet(out MotionBlur motionBlur) && motionBlur != null)
                motionBlur.active = enableMotionBlur;
        }
    }

    void ApplySoundSettings()
    {
        float effectiveVolume = Mathf.Max(0f, masterVolume * 0.5f);
        AudioListener.volume = effectiveVolume;
        AudioListener.pause = false;

        var audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null)
                continue;

            if (source.GetComponent("MainMenuMusicController") == null)
                continue;

            source.volume = Mathf.Clamp01(menuMusicVolume);
        }
    }

    void ApplyControlSettings()
    {
        var controllers = FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].SetMouseSensitivity(mouseSensitivity);
        }
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this)
            return;

        ApplyControlSettings();
        ApplyVisualSettings();
    }

    public void SaveSettingsToDisk()
    {
        if (string.IsNullOrEmpty(_settingsFilePath))
            _settingsFilePath = Path.Combine(Application.persistentDataPath, "player-settings.json");

        var data = new PlayerSettingsData
        {
            enableVSync = enableVSync,
            targetFrameRate = targetFrameRate,
            qualityLevel = qualityLevel,
            shadowQuality = (int)shadowQuality,
            enableMotionBlur = enableMotionBlur,
            masterVolume = masterVolume,
            menuMusicVolume = menuMusicVolume,
            mouseSensitivity = mouseSensitivity
        };

        string json = JsonUtility.ToJson(data, true);
        WriteJsonWithBackup(_settingsFilePath, json);
    }

    static void WriteJsonWithBackup(string path, string json)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string backupPath = path + ".bak";
        if (File.Exists(path))
            File.Copy(path, backupPath, true);

        File.WriteAllText(path, json);
    }

    public void LoadSettingsFromDisk()
    {
        if (string.IsNullOrEmpty(_settingsFilePath))
            _settingsFilePath = Path.Combine(Application.persistentDataPath, "player-settings.json");

        if (!File.Exists(_settingsFilePath))
            return;

        string json = File.ReadAllText(_settingsFilePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        var data = JsonUtility.FromJson<PlayerSettingsData>(json);
        if (data == null)
            return;

        enableVSync = data.enableVSync;
        targetFrameRate = Mathf.Max(30, data.targetFrameRate);
        qualityLevel = Mathf.Max(0, data.qualityLevel);
        shadowQuality = Enum.IsDefined(typeof(EngineShadowQuality), data.shadowQuality)
            ? (EngineShadowQuality)data.shadowQuality
            : EngineShadowQuality.All;
        if (json.Contains("\"enableMotionBlur\""))
            enableMotionBlur = data.enableMotionBlur;
        masterVolume = Mathf.Clamp(data.masterVolume, 0f, 4f);
        menuMusicVolume = data.menuMusicVolume < 0f ? 0.75f : Mathf.Clamp01(data.menuMusicVolume);
        mouseSensitivity = data.mouseSensitivity <= 0f ? 2f : Mathf.Clamp(data.mouseSensitivity, 0.2f, 8f);
    }

    [ContextMenu("Delete Saved Settings File")]
    public void DeleteSavedSettingsFile()
    {
        if (string.IsNullOrEmpty(_settingsFilePath))
            _settingsFilePath = Path.Combine(Application.persistentDataPath, "player-settings.json");

        if (File.Exists(_settingsFilePath))
            File.Delete(_settingsFilePath);
    }
}
