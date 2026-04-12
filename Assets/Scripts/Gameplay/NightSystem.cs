using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class NightSystem : MonoBehaviour
{
    [Serializable]
    public class PlayerDeathEvent : UnityEvent<string> { }

    public enum NightDay
    {
        Day1 = 1,
        Day2 = 2,
        Day3 = 3
    }

    [Serializable]
    class SaveSlotData
    {
        public bool hasData;
        public int currentNight = 1;
        public int deathCount;
        public string createdAtUtc;
        public string lastSavedAtUtc;
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

    [Serializable]
    public class DayActivationSet
    {
        public NightDay day = NightDay.Day1;
        public List<GameObject> objectsToEnableForDay = new List<GameObject>();
    }

    [Serializable]
    public class DaySettings
    {
        [Min(1f)] public float requiredClosedEyesSeconds = 120f;
        [Min(0f)] public float sleepProgressDecayPerSecond = 0f;
        [Min(0.1f)] public float baseDifficultyMultiplier = 1f;
        [Min(0f)] public float flashlightDrainScale = 1f;
        public AnimationCurve difficultyOverNight =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2f));
    }

    [Serializable]
    public class SleepTriggeredNightEvent
    {
        public string id = "Event";
        [Range(0f, 1f)] public float triggerSleepPercent = 0.2f;
        public NightEvent eventTemplate;
        public bool triggerAtRandomSleepPercent;
        public Vector2 randomSleepPercentRange = new Vector2(0.1f, 0.9f);

        [FormerlySerializedAs("onlyTriggerOnSpecificDay"), HideInInspector]
        public bool legacyOnlyTriggerOnSpecificDay;
        [FormerlySerializedAs("specificDay"), HideInInspector]
        public NightDay legacySpecificDay = NightDay.Day1;
        [FormerlySerializedAs("triggerOnce"), HideInInspector]
        public bool legacyTriggerOnce = true;

        [NonSerialized] public bool hasTriggered;
        [NonSerialized] float _resolvedTriggerSleepPercent;

        public float ActiveTriggerSleepPercent => triggerAtRandomSleepPercent
            ? _resolvedTriggerSleepPercent
            : Mathf.Clamp01(triggerSleepPercent);

        public void Trigger()
        {
            hasTriggered = true;

            if (eventTemplate != null)
                eventTemplate.TriggerEvent();
        }

        public void ResetState()
        {
            hasTriggered = false;
            _resolvedTriggerSleepPercent = ResolveTriggerSleepPercent();

            if (eventTemplate != null)
                eventTemplate.ResetEventState();
        }

        float ResolveTriggerSleepPercent()
        {
            if (!triggerAtRandomSleepPercent)
                return Mathf.Clamp01(triggerSleepPercent);

            float min = Mathf.Clamp01(Mathf.Min(randomSleepPercentRange.x, randomSleepPercentRange.y));
            float max = Mathf.Clamp01(Mathf.Max(randomSleepPercentRange.x, randomSleepPercentRange.y));
            return UnityEngine.Random.Range(min, max);
        }
    }

    public static NightSystem Instance { get; private set; }

    [Header("Night Runtime")]
    [SerializeField] bool startNightOnAwake = true;
    [SerializeField] NightDay currentDay = NightDay.Day1;
    [SerializeField] PlayerSleepSystem sleepSystem;
    [SerializeField] UnityEvent onNightCompleted;

    [Header("Night Completion Flow")]
    [SerializeField] bool returnToMenuOnNightCompleted = true;
    [SerializeField] bool resetToDayOneOnNightCompleted = true;
    [SerializeField] TemporaryMainMenu mainMenu;

    [Header("Persistent Save")]
    [SerializeField, Range(0, 2)] int activeSaveSlotIndex = 0;
    [SerializeField] string saveFileName = "save-slots.json";

    [Header("Player Death")]
    [SerializeField] bool fullResetOnPlayerDeath = true;
    [SerializeField] UnityEvent onPlayerDied;
    [SerializeField] PlayerDeathEvent onPlayerDiedBy;

    [Header("Day Title Popup")]
    [SerializeField] bool showDayTitleOnNightStart = true;
    [SerializeField] string dayTitlePrefix = "Day ";
    [SerializeField] string dayTitleFormatOverride = string.Empty;
    [SerializeField, Min(0.1f)] float dayTitleDurationSeconds = 2.4f;
    [SerializeField, Min(12)] int dayTitleFontSize = 64;
    [SerializeField] Color dayTitleColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] Vector2 dayTitleAnchoredPosition = new Vector2(0f, 140f);

    [Header("Night Activations")]
    [SerializeField] List<GameObject> globalObjectsToEnableEveryNight = new List<GameObject>();
    [SerializeField] List<DayActivationSet> dayActivationSets = new List<DayActivationSet>();

    [Header("Day Settings")]
    [SerializeField] DaySettings day1 = new DaySettings
    {
        requiredClosedEyesSeconds = 90f,
        sleepProgressDecayPerSecond = 0f,
        baseDifficultyMultiplier = 1f,
        flashlightDrainScale = 1f,
        difficultyOverNight = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1.25f))
    };

    [SerializeField] DaySettings day2 = new DaySettings
    {
        requiredClosedEyesSeconds = 120f,
        sleepProgressDecayPerSecond = 0.02f,
        baseDifficultyMultiplier = 1.2f,
        flashlightDrainScale = 1.2f,
        difficultyOverNight = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1.55f))
    };

    [SerializeField] DaySettings day3 = new DaySettings
    {
        requiredClosedEyesSeconds = 150f,
        sleepProgressDecayPerSecond = 0.04f,
        baseDifficultyMultiplier = 1.4f,
        flashlightDrainScale = 1.4f,
        difficultyOverNight = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1.9f))
    };

    [Header("Night Events (Sleep % Triggers)")]
    [FormerlySerializedAs("nightEvents")]
    [SerializeField] List<SleepTriggeredNightEvent> day1NightEvents = new List<SleepTriggeredNightEvent>();
    [SerializeField] List<SleepTriggeredNightEvent> day2NightEvents = new List<SleepTriggeredNightEvent>();
    [SerializeField] List<SleepTriggeredNightEvent> day3NightEvents = new List<SleepTriggeredNightEvent>();

    bool _isNightRunning;
    bool _hasProcessedDeathThisNight;
    GameObject _dayTitleCanvasObject;
    Text _dayTitleText;
    Coroutine _dayTitleRoutine;
    SaveDataRoot _saveData;
    string _saveFilePath;

    public int ActiveSaveSlotIndex => activeSaveSlotIndex;
    public bool IsNightRunning => _isNightRunning;
    public NightDay CurrentDay => currentDay;
    public float NightProgress => sleepSystem != null ? sleepSystem.SleepProgress01 : 0f;
    public float DifficultyMultiplier => ActiveSettings.baseDifficultyMultiplier * ActiveSettings.difficultyOverNight.Evaluate(NightProgress);
    public float FlashlightDrainMultiplier => Mathf.Max(0f, DifficultyMultiplier * ActiveSettings.flashlightDrainScale);
    public string LastKillerId { get; private set; } = string.Empty;

    DaySettings ActiveSettings => GetDaySettings(currentDay);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        MigrateLegacyDayAssignments();
    }

    void Start()
    {
        if (sleepSystem == null)
            sleepSystem = FindFirstObjectByType<PlayerSleepSystem>();

        if (mainMenu == null)
            mainMenu = FindFirstObjectByType<TemporaryMainMenu>();

        _saveFilePath = System.IO.Path.Combine(Application.persistentDataPath, saveFileName);
        LoadSaveDataFromDisk();
        activeSaveSlotIndex = Mathf.Clamp(_saveData.activeSlotIndex, 0, 2);
        EnsureSaveSlotExists(activeSaveSlotIndex);
        PullDayFromSession();

        if (startNightOnAwake)
            StartNight(resetTime: true);
    }

    public void StartNight(bool resetTime)
    {
        PullDayFromSession();

        if (resetTime)
            ResetNightTime();

        _isNightRunning = true;
        _hasProcessedDeathThisNight = false;

        ResetRuntimeThreatState();
        ResetEventsForAllDays();
        ApplyNightActivations();

        ApplyGlobalSleepProgressSettings();
        ShowDayTitlePopup();
        PushDayToSession();
    }

    void Update()
    {
        if (!_isNightRunning)
            return;

        ApplyGlobalSleepProgressSettings();

        EvaluateSleepEvents();

        if (sleepSystem != null && sleepSystem.SleepProgress01 >= 1f)
            CompleteNight();
    }

    void ResetRuntimeThreatState()
    {
        var monsters = FindObjectsByType<FindHerMonsterController>(FindObjectsSortMode.None);
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] != null)
                monsters[i].ResetForNightStart();
        }

        var breathers = FindObjectsByType<BreatherMonsterController>(FindObjectsSortMode.None);
        for (int i = 0; i < breathers.Length; i++)
        {
            if (breathers[i] != null)
                breathers[i].ResetForNightStart();
        }
    }

    void CompleteNight()
    {
        _isNightRunning = false;
        onNightCompleted?.Invoke();

        if (resetToDayOneOnNightCompleted)
            currentDay = NightDay.Day1;

        PushDayToSession();

        if (returnToMenuOnNightCompleted && mainMenu != null)
            mainMenu.ReturnToMenu();
    }

    public void StopNight()
    {
        _isNightRunning = false;
    }

    public void HandlePlayerDeath(string killerId)
    {
        if (_hasProcessedDeathThisNight)
            return;

        _hasProcessedDeathThisNight = true;
        _isNightRunning = false;
        LastKillerId = string.IsNullOrWhiteSpace(killerId) ? "Unknown" : killerId;

        IncrementDeathForActiveSlot();

        onPlayerDied?.Invoke();
        onPlayerDiedBy?.Invoke(LastKillerId);

        if (mainMenu == null)
            mainMenu = FindFirstObjectByType<TemporaryMainMenu>();

        if (mainMenu == null)
            return;

        if (fullResetOnPlayerDeath)
            mainMenu.ReturnToMenuFreshBoot();
        else
            mainMenu.ReturnToMenu();
    }

    void ShowDayTitlePopup()
    {
        if (!showDayTitleOnNightStart)
            return;

        EnsureDayTitleUi();
        if (_dayTitleText == null)
            return;

        _dayTitleText.fontSize = dayTitleFontSize;
        _dayTitleText.color = dayTitleColor;
        _dayTitleText.rectTransform.anchoredPosition = dayTitleAnchoredPosition;

        string label = !string.IsNullOrWhiteSpace(dayTitleFormatOverride)
            ? dayTitleFormatOverride.Replace("{DAY}", ((int)currentDay).ToString())
            : $"{dayTitlePrefix}{(int)currentDay}";

        _dayTitleText.text = label;
        _dayTitleText.enabled = true;

        if (_dayTitleRoutine != null)
            StopCoroutine(_dayTitleRoutine);

        _dayTitleRoutine = StartCoroutine(HideDayTitleAfterDelay());
    }

    IEnumerator HideDayTitleAfterDelay()
    {
        float delay = Mathf.Max(0.1f, dayTitleDurationSeconds);
        yield return new WaitForSeconds(delay);

        if (_dayTitleText != null)
            _dayTitleText.enabled = false;

        _dayTitleRoutine = null;
    }

    void EnsureDayTitleUi()
    {
        if (_dayTitleText != null)
            return;

        var existing = GameObject.Find("NightDayTitleCanvas");
        if (existing != null)
            _dayTitleCanvasObject = existing;

        if (_dayTitleCanvasObject == null)
            _dayTitleCanvasObject = new GameObject("NightDayTitleCanvas");

        if (_dayTitleCanvasObject.GetComponent<Canvas>() == null)
        {
            var canvas = _dayTitleCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2500;
        }

        if (_dayTitleCanvasObject.GetComponent<CanvasScaler>() == null)
            _dayTitleCanvasObject.AddComponent<CanvasScaler>();

        if (_dayTitleCanvasObject.GetComponent<GraphicRaycaster>() == null)
            _dayTitleCanvasObject.AddComponent<GraphicRaycaster>();

        Transform titleTransform = _dayTitleCanvasObject.transform.Find("NightDayTitleText");
        if (titleTransform == null)
        {
            var textGo = new GameObject("NightDayTitleText");
            textGo.transform.SetParent(_dayTitleCanvasObject.transform, false);
            _dayTitleText = textGo.AddComponent<Text>();
        }
        else
        {
            _dayTitleText = titleTransform.GetComponent<Text>();
            if (_dayTitleText == null)
                _dayTitleText = titleTransform.gameObject.AddComponent<Text>();
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _dayTitleText.font = font;
        _dayTitleText.alignment = TextAnchor.MiddleCenter;
        _dayTitleText.enabled = false;

        RectTransform rect = _dayTitleText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 120f);
        rect.anchoredPosition = dayTitleAnchoredPosition;
    }

    void ApplyNightActivations()
    {
        for (int i = 0; i < globalObjectsToEnableEveryNight.Count; i++)
        {
            if (globalObjectsToEnableEveryNight[i] != null)
                globalObjectsToEnableEveryNight[i].SetActive(true);
        }

        for (int i = 0; i < dayActivationSets.Count; i++)
        {
            DayActivationSet set = dayActivationSets[i];
            if (set == null || set.objectsToEnableForDay == null)
                continue;

            bool enableSet = set.day == currentDay;
            for (int j = 0; j < set.objectsToEnableForDay.Count; j++)
            {
                GameObject go = set.objectsToEnableForDay[j];
                if (go != null)
                    go.SetActive(enableSet);
            }
        }
    }

    public void ResetNightTime()
    {
        if (sleepSystem != null)
            sleepSystem.ResetSleepProgress();
    }

    public void SetDay(NightDay day)
    {
        currentDay = day;
        PushDayToSession();
    }

    public void SetDayAndRestart(NightDay day)
    {
        currentDay = day;
        PushDayToSession();
        StartNight(resetTime: true);
    }

    void EvaluateSleepEvents()
    {
        if (sleepSystem == null || !sleepSystem.IsInBag)
            return;

        float sleepProgress = sleepSystem.SleepProgress01;
        List<SleepTriggeredNightEvent> activeEvents = GetEventsForDay(currentDay);

        for (int i = 0; i < activeEvents.Count; i++)
        {
            var nightEventEntry = activeEvents[i];
            if (nightEventEntry == null || nightEventEntry.hasTriggered)
                continue;

            if (sleepProgress < nightEventEntry.ActiveTriggerSleepPercent)
                continue;

            nightEventEntry.Trigger();
        }
    }

    void MigrateLegacyDayAssignments()
    {
        if (day1NightEvents == null || day1NightEvents.Count == 0)
            return;

        day2NightEvents ??= new List<SleepTriggeredNightEvent>();
        day3NightEvents ??= new List<SleepTriggeredNightEvent>();

        for (int i = day1NightEvents.Count - 1; i >= 0; i--)
        {
            var entry = day1NightEvents[i];
            if (entry == null || !entry.legacyOnlyTriggerOnSpecificDay)
                continue;

            if (entry.legacySpecificDay == NightDay.Day2)
            {
                day2NightEvents.Add(entry);
                day1NightEvents.RemoveAt(i);
            }
            else if (entry.legacySpecificDay == NightDay.Day3)
            {
                day3NightEvents.Add(entry);
                day1NightEvents.RemoveAt(i);
            }

            entry.legacyOnlyTriggerOnSpecificDay = false;
        }
    }

    void ResetEventsForAllDays()
    {
        ResetEventList(day1NightEvents);
        ResetEventList(day2NightEvents);
        ResetEventList(day3NightEvents);
    }

    static void ResetEventList(List<SleepTriggeredNightEvent> events)
    {
        if (events == null)
            return;

        for (int i = 0; i < events.Count; i++)
            events[i]?.ResetState();
    }

    List<SleepTriggeredNightEvent> GetEventsForDay(NightDay day)
    {
        switch (day)
        {
            case NightDay.Day2: return day2NightEvents;
            case NightDay.Day3: return day3NightEvents;
            default: return day1NightEvents;
        }
    }

    void ApplyGlobalSleepProgressSettings()
    {
        if (sleepSystem == null)
            return;

        float gainPerSecond = 1f / Mathf.Max(1f, ActiveSettings.requiredClosedEyesSeconds);
        sleepSystem.ConfigureSleepProgressRates(gainPerSecond, ActiveSettings.sleepProgressDecayPerSecond);
    }

    void PullDayFromSession()
    {
        EnsureSaveSlotExists(activeSaveSlotIndex);

        int dayIndex = _saveData.slots[activeSaveSlotIndex].currentNight;

        if (Enum.IsDefined(typeof(NightDay), dayIndex))
            currentDay = (NightDay)dayIndex;
        else
            currentDay = NightDay.Day1;
    }

    void PushDayToSession()
    {
        EnsureSaveSlotExists(activeSaveSlotIndex);

        SaveSlotData slot = _saveData.slots[activeSaveSlotIndex];
        slot.currentNight = (int)currentDay;
        slot.lastSavedAtUtc = DateTime.UtcNow.ToString("O");
        _saveData.activeSlotIndex = activeSaveSlotIndex;
        SaveSaveDataToDisk();
    }

    void IncrementDeathForActiveSlot()
    {
        EnsureSaveSlotExists(activeSaveSlotIndex);

        SaveSlotData slot = _saveData.slots[activeSaveSlotIndex];
        slot.deathCount = Mathf.Max(0, slot.deathCount + 1);
        slot.lastSavedAtUtc = DateTime.UtcNow.ToString("O");
        SaveSaveDataToDisk();
    }

    void EnsureSaveSlotExists(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return;

        if (_saveData == null)
            LoadSaveDataFromDisk();

        if (_saveData.slots[slotIndex] != null && _saveData.slots[slotIndex].hasData)
            return;

        CreateOrOverrideSaveSlot(slotIndex, 1);
    }

    public void SelectSaveSlot(int slotIndex, bool createIfMissing)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return;

        activeSaveSlotIndex = slotIndex;
        _saveData.activeSlotIndex = activeSaveSlotIndex;

        if (createIfMissing)
            EnsureSaveSlotExists(activeSaveSlotIndex);

        PullDayFromSession();
        SaveSaveDataToDisk();
    }

    public void CreateOrOverrideSaveSlot(int slotIndex, int startingNight)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return;

        string now = DateTime.UtcNow.ToString("O");
        SaveSlotData slot = _saveData.slots[slotIndex] ?? new SaveSlotData();
        slot.hasData = true;
        slot.currentNight = Mathf.Max(1, startingNight);
        slot.deathCount = 0;
        slot.createdAtUtc = now;
        slot.lastSavedAtUtc = now;
        _saveData.slots[slotIndex] = slot;
        _saveData.activeSlotIndex = slotIndex;
        activeSaveSlotIndex = slotIndex;
        SaveSaveDataToDisk();
    }

    public void DeleteSaveSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return;

        _saveData.slots[slotIndex] = new SaveSlotData();
        if (_saveData.activeSlotIndex == slotIndex)
            _saveData.activeSlotIndex = 0;

        activeSaveSlotIndex = _saveData.activeSlotIndex;
        SaveSaveDataToDisk();
    }

    public int GetSaveSlotCurrentNight(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return 1;

        return Mathf.Max(1, _saveData.slots[slotIndex].currentNight);
    }

    public int GetSaveSlotDeathCount(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return 0;

        return Mathf.Max(0, _saveData.slots[slotIndex].deathCount);
    }

    public string GetSaveSlotCreatedAtUtc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return string.Empty;

        return _saveData.slots[slotIndex].createdAtUtc ?? string.Empty;
    }

    public bool HasSaveSlotData(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return false;

        return _saveData.slots[slotIndex] != null && _saveData.slots[slotIndex].hasData;
    }

    void LoadSaveDataFromDisk()
    {
        if (System.IO.File.Exists(_saveFilePath))
        {
            string json = System.IO.File.ReadAllText(_saveFilePath);
            if (!string.IsNullOrWhiteSpace(json))
                _saveData = JsonUtility.FromJson<SaveDataRoot>(json);
        }

        if (_saveData == null)
            _saveData = new SaveDataRoot();

        if (_saveData.slots == null || _saveData.slots.Length != 3)
            _saveData.slots = new SaveSlotData[3] { new SaveSlotData(), new SaveSlotData(), new SaveSlotData() };

        for (int i = 0; i < _saveData.slots.Length; i++)
        {
            if (_saveData.slots[i] == null)
                _saveData.slots[i] = new SaveSlotData();
        }

        _saveData.activeSlotIndex = Mathf.Clamp(_saveData.activeSlotIndex, 0, 2);

        if (!TryMigrateLegacyPlayerPrefsSaves())
            SaveSaveDataToDisk();
    }

    bool TryMigrateLegacyPlayerPrefsSaves()
    {
        bool migratedAny = false;
        for (int i = 0; i < 3; i++)
        {
            string hasKey = $"SaveSlots.{i}.HasData";
            if (PlayerPrefs.GetInt(hasKey, 0) != 1)
                continue;

            SaveSlotData slot = _saveData.slots[i] ?? new SaveSlotData();
            slot.hasData = true;
            slot.currentNight = Mathf.Max(1, PlayerPrefs.GetInt($"SaveSlots.{i}.CurrentNight", 1));
            slot.deathCount = Mathf.Max(0, PlayerPrefs.GetInt($"SaveSlots.{i}.Deaths", 0));
            slot.createdAtUtc = PlayerPrefs.GetString($"SaveSlots.{i}.CreatedAtUtc", DateTime.UtcNow.ToString("O"));
            slot.lastSavedAtUtc = PlayerPrefs.GetString($"SaveSlots.{i}.LastSavedAtUtc", slot.createdAtUtc);
            _saveData.slots[i] = slot;
            migratedAny = true;
        }

        if (migratedAny)
        {
            _saveData.activeSlotIndex = Mathf.Clamp(PlayerPrefs.GetInt("SaveSlots.ActiveIndex", _saveData.activeSlotIndex), 0, 2);
            SaveSaveDataToDisk();
        }

        return migratedAny;
    }

    void SaveSaveDataToDisk()
    {
        if (_saveData == null)
            _saveData = new SaveDataRoot();

        _saveData.activeSlotIndex = Mathf.Clamp(_saveData.activeSlotIndex, 0, 2);
        string json = JsonUtility.ToJson(_saveData, true);
        System.IO.File.WriteAllText(_saveFilePath, json);
    }

    DaySettings GetDaySettings(NightDay day)
    {
        switch (day)
        {
            case NightDay.Day2: return day2;
            case NightDay.Day3: return day3;
            default: return day1;
        }
    }
}
