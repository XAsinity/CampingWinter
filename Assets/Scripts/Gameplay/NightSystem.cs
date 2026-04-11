using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

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

    [Header("Player Death")]
    [SerializeField] bool fullResetOnPlayerDeath = true;
    [SerializeField] UnityEvent onPlayerDied;
    [SerializeField] PlayerDeathEvent onPlayerDiedBy;

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

        if (startNightOnAwake)
            StartNight(resetTime: true);
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

    public void StartNight(bool resetTime)
    {
        if (resetTime)
            ResetNightTime();

        _isNightRunning = true;

        ResetEventsForAllDays();
        ApplyNightActivations();

        ApplyGlobalSleepProgressSettings();
    }

    void CompleteNight()
    {
        _isNightRunning = false;
        onNightCompleted?.Invoke();

        if (resetToDayOneOnNightCompleted)
            currentDay = NightDay.Day1;

        if (returnToMenuOnNightCompleted && mainMenu != null)
            mainMenu.ReturnToMenu();
    }

    public void StopNight()
    {
        _isNightRunning = false;
    }

    public void HandlePlayerDeath(string killerId)
    {
        _isNightRunning = false;
        LastKillerId = string.IsNullOrWhiteSpace(killerId) ? "Unknown" : killerId;

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
    }

    public void SetDayAndRestart(NightDay day)
    {
        currentDay = day;
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
