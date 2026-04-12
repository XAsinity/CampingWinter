using System;
using System.IO;
using UnityEngine;

public class PlayerSettings : MonoBehaviour
{
    [Serializable]
    class PlayerSettingsData
    {
        public bool enableVSync;
        public int targetFrameRate;
        public int qualityLevel;
        public int shadowQuality;
        public float masterVolume;
    }

    [Header("Performance")]
    [SerializeField] bool enableVSync;
    [SerializeField, Min(30)] int targetFrameRate = 60;

    [Header("Quality")]
    [SerializeField] int qualityLevel = 2;
    [SerializeField] ShadowQuality shadowQuality = ShadowQuality.All;

    [Header("Sound")]
    [SerializeField, Range(0f, 4f)] float masterVolume = 1f;

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

    public ShadowQuality ShadowQualitySetting
    {
        get => shadowQuality;
        set => shadowQuality = value;
    }

    public float MasterVolume
    {
        get => masterVolume;
        set => masterVolume = Mathf.Clamp(value, 0f, 4f);
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

        if (loadSavedSettingsOnStart)
            LoadSettingsFromDisk();
    }

    void Start()
    {
        ApplyAllSettings(saveToDisk: false);
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ApplyAllSettings();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && saveOnApplicationPause)
            SaveSettingsToDisk();
    }

    void OnApplicationQuit()
    {
        SaveSettingsToDisk();
    }

    public void ApplyAllSettings(bool saveToDisk = true)
    {
        ApplyPerformanceSettings();
        ApplyQualitySettings();
        ApplySoundSettings();

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

    void ApplySoundSettings()
    {
        float effectiveVolume = Mathf.Max(0f, masterVolume * 0.5f);
        AudioListener.volume = effectiveVolume;
        AudioListener.pause = false;
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
            masterVolume = masterVolume
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(_settingsFilePath, json);
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
        shadowQuality = Enum.IsDefined(typeof(ShadowQuality), data.shadowQuality)
            ? (ShadowQuality)data.shadowQuality
            : ShadowQuality.All;
        masterVolume = Mathf.Clamp(data.masterVolume, 0f, 4f);
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
