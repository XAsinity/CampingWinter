using System;
using System.IO;
using UnityEngine;

public class SystemsRootBootstrap : MonoBehaviour
{
    [Serializable]
    class SaveSlotData
    {
        public bool hasData;
        public int currentNight = 1;
        public int deathCount;
        public string createdAtUtc = string.Empty;
        public string lastSavedAtUtc = string.Empty;
    }

    [Serializable]
    class SaveDataRoot
    {
        public int activeSlotIndex;
        public SaveSlotData[] slots = new SaveSlotData[3]
        {
            new SaveSlotData(),
            new SaveSlotData(),
            new SaveSlotData()
        };
    }

    static SystemsRootBootstrap _instance;

    [Header("Auto Systems")]
    [SerializeField] bool ensurePlayerSettings = true;
    [SerializeField] bool ensureDataFilesOnBoot = true;

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

        if (ensureDataFilesOnBoot)
            EnsureDataFiles();
    }

    void EnsureDataFiles()
    {
        string settingsPath = Path.Combine(Application.persistentDataPath, "player-settings.json");
        if (!File.Exists(settingsPath))
        {
            var settings = GetComponent<PlayerSettings>();
            if (settings != null)
                settings.SaveSettingsToDisk();
        }

        string savesPath = Path.Combine(Application.persistentDataPath, "save-slots.json");
        if (!File.Exists(savesPath))
        {
            var data = new SaveDataRoot();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savesPath, json);
        }
    }
}
