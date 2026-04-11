using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FindHerMonsterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RadioNightEvent radioEvent;
    [SerializeField] NightSystem nightSystemOverride;
    [SerializeField] PlayerFlashlight flashlightOverride;
    [SerializeField] PlayerSleepSystem sleepSystemOverride;
    [SerializeField] Transform monsterTransform;
    [SerializeField] Transform detectionTargetOverride;

    [Header("Failure")]
    [SerializeField] UnityEvent onFailedFind;

    [Header("Activation")]
    [SerializeField] bool onlyActivateOnDay1 = true;
    [SerializeField, Min(1f)] float closedEyesSecondsPerAgitation = 20f;

    [Header("Spawn Odds")]
    [SerializeField, Min(0.5f)] float spawnRollIntervalSeconds = 10f;
    [SerializeField, Range(0f, 1f)] float baseSpawnChancePerRoll = 0.08f;
    [SerializeField, Range(0f, 1f)] float nightProgressSpawnBonus = 0.18f;
    [SerializeField, Range(0f, 1f)] float agitationSpawnBonusPerStack = 0.06f;

    [Header("Find-Her Time Pressure")]
    [SerializeField, Min(1f)] float baseFindHerTimeSeconds = 18f;
    [SerializeField, Range(0f, 1f)] float difficultyTimeReductionPerMultiplier = 0.42f;
    [SerializeField, Range(0f, 1f)] float agitationTimeReductionPerStack = 0.025f;
    [SerializeField, Range(0.05f, 1f)] float minFindHerTimeMultiplier = 0.25f;

    [Header("Monster Spawns")]
    [SerializeField] List<Transform> spawnPoints = new List<Transform>();

    [Header("Flashlight Detection")]
    [SerializeField, Min(0.5f)] float maxDetectionDistance = 20f;
    [SerializeField, Min(0f)] float extraAnglePadding = 2f;
    [SerializeField, Min(0f)] float requiredContinuousSpotSeconds = 0.15f;
    [SerializeField] bool requireDirectRaycastHit = true;
    [SerializeField] LayerMask visibilityMask = ~0;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] bool liveIsArmed;
    [SerializeField] bool liveHuntActive;
    [SerializeField] int liveAgitation;
    [SerializeField] float liveCurrentSpawnChance;
    [SerializeField] float liveCurrentFindHerTime;

    bool _isArmed;
    bool _huntActive;
    int _agitation;
    float _closedEyesTimer;
    float _spawnRollTimer;
    float _spotTimer;
    bool _resolvedCurrentRound;

    PlayerFlashlight _flashlight;
    PlayerSleepSystem _sleepSystem;
    Light _flashlightLight;
    Renderer[] _monsterRenderers;
    Collider[] _monsterColliders;

    void Awake()
    {
        if (radioEvent == null)
            radioEvent = GetComponent<RadioNightEvent>();

        if (monsterTransform == null)
            monsterTransform = transform;

        _flashlight = flashlightOverride;
        if (_flashlight == null)
            _flashlight = FindFirstObjectByType<PlayerFlashlight>();

        _sleepSystem = sleepSystemOverride;
        if (_sleepSystem == null)
            _sleepSystem = FindFirstObjectByType<PlayerSleepSystem>();

        CacheMonsterComponents();
        SetMonsterVisible(false);
    }

    void OnEnable()
    {
        if (radioEvent != null)
        {
            radioEvent.RadioEnded += HandleRadioEnded;
        }
    }

    void OnDisable()
    {
        if (radioEvent != null)
        {
            radioEvent.RadioEnded -= HandleRadioEnded;
        }
    }

    void Update()
    {
        SyncLiveDebugValues();

        if (!_huntActive)
        {
            UpdateAgitationFromSleepChecks();
            TryRollSpawn();
            return;
        }

        if (IsMonsterInFlashlightView())
        {
            _spotTimer += Time.deltaTime;
            if (_spotTimer >= requiredContinuousSpotSeconds)
            {
                if (radioEvent != null)
                {
                    _resolvedCurrentRound = true;
                    radioEvent.ResolveMonsterObjective();
                }

                EndHunt();
            }
        }
        else
        {
            _spotTimer = 0f;
        }
    }

    void HandleRadioEnded(bool wasMonsterRound)
    {
        if (!_isArmed && !wasMonsterRound && ShouldUnlockForCurrentDay())
        {
            _isArmed = true;
            _agitation = 0;
            _closedEyesTimer = 0f;
            _spawnRollTimer = 0f;
        }

        if (wasMonsterRound && !_resolvedCurrentRound)
            HandleFailedFind();

        _resolvedCurrentRound = false;

        EndHunt();
    }

    bool ShouldUnlockForCurrentDay()
    {
        if (!onlyActivateOnDay1)
            return true;

        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        return nightSystem == null || nightSystem.CurrentDay == NightSystem.NightDay.Day1;
    }

    void UpdateAgitationFromSleepChecks()
    {
        if (!_isArmed)
            return;

        if (_sleepSystem == null)
            _sleepSystem = sleepSystemOverride != null ? sleepSystemOverride : FindFirstObjectByType<PlayerSleepSystem>();

        if (_sleepSystem == null || !_sleepSystem.IsInBag)
        {
            _closedEyesTimer = 0f;
            return;
        }

        if (!_sleepSystem.IsHoldingSleep)
        {
            _closedEyesTimer = 0f;
            return;
        }

        _closedEyesTimer += Time.deltaTime;
        while (_closedEyesTimer >= closedEyesSecondsPerAgitation)
        {
            _closedEyesTimer -= closedEyesSecondsPerAgitation;
            _agitation++;
        }
    }

    void TryRollSpawn()
    {
        if (!_isArmed || radioEvent == null || radioEvent.IsRadioOn)
            return;

        if (!IsPlayerLayingInBag())
        {
            _spawnRollTimer = 0f;
            return;
        }

        _spawnRollTimer += Time.deltaTime;
        if (_spawnRollTimer < spawnRollIntervalSeconds)
            return;

        _spawnRollTimer = 0f;

        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        float nightProgress = nightSystem != null ? nightSystem.NightProgress : 0f;

        float chance = baseSpawnChancePerRoll
            + (nightProgress * nightProgressSpawnBonus)
            + (_agitation * agitationSpawnBonusPerStack);

        chance = Mathf.Clamp01(chance);
        liveCurrentSpawnChance = chance;
        if (Random.value <= chance)
            StartHunt();
    }

    bool IsPlayerLayingInBag()
    {
        if (_sleepSystem == null)
            _sleepSystem = sleepSystemOverride != null ? sleepSystemOverride : FindFirstObjectByType<PlayerSleepSystem>();

        return _sleepSystem != null && _sleepSystem.IsInBag && _sleepSystem.IsHoldingSleep;
    }

    void StartHunt()
    {
        if (!_isArmed || monsterTransform == null || radioEvent == null)
            return;

        float timeLimitSeconds = GetCurrentFindHerTimeLimitSeconds();

        if (!radioEvent.TryStartMonsterRound(timeLimitSeconds))
            return;

        MoveMonsterToRandomSpawn();
        SetMonsterVisible(true);
        _huntActive = true;
        _resolvedCurrentRound = false;
        liveCurrentFindHerTime = timeLimitSeconds;
    }

    void EndHunt()
    {
        _huntActive = false;
        _spotTimer = 0f;
        SetMonsterVisible(false);
    }

    float GetCurrentFindHerTimeLimitSeconds()
    {
        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        float difficultyMultiplier = nightSystem != null ? nightSystem.DifficultyMultiplier : 1f;

        float difficultyReduction = Mathf.Max(0f, difficultyMultiplier - 1f) * difficultyTimeReductionPerMultiplier;
        float agitationReduction = _agitation * agitationTimeReductionPerStack;

        float totalMultiplier = 1f - difficultyReduction - agitationReduction;
        totalMultiplier = Mathf.Clamp(totalMultiplier, minFindHerTimeMultiplier, 1f);

        return baseFindHerTimeSeconds * totalMultiplier;
    }

    void HandleFailedFind()
    {
        onFailedFind?.Invoke();

        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        if (nightSystem != null)
            nightSystem.HandlePlayerDeath("Eveline");
    }

    void SyncLiveDebugValues()
    {
        liveIsArmed = _isArmed;
        liveHuntActive = _huntActive;
        liveAgitation = _agitation;

        if (_huntActive)
            return;

        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        float nightProgress = nightSystem != null ? nightSystem.NightProgress : 0f;

        float chance = baseSpawnChancePerRoll
            + (nightProgress * nightProgressSpawnBonus)
            + (_agitation * agitationSpawnBonusPerStack);

        liveCurrentSpawnChance = Mathf.Clamp01(chance);
        liveCurrentFindHerTime = GetCurrentFindHerTimeLimitSeconds();
    }

    void MoveMonsterToRandomSpawn()
    {
        var validSpawns = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null)
                validSpawns.Add(spawnPoints[i]);
        }

        if (validSpawns.Count == 0)
            return;

        Transform spawn = validSpawns[Random.Range(0, validSpawns.Count)];
        monsterTransform.position = spawn.position;
        monsterTransform.rotation = spawn.rotation;
    }

    bool IsMonsterInFlashlightView()
    {
        if (_flashlight == null)
            _flashlight = FindFirstObjectByType<PlayerFlashlight>();

        if (_flashlight == null || !_flashlight.IsOn)
            return false;

        _flashlightLight = _flashlight.FlashlightLight;
        if (_flashlightLight == null || !_flashlightLight.enabled)
            return false;

        Vector3 targetPosition = detectionTargetOverride != null
            ? detectionTargetOverride.position
            : monsterTransform.position;

        Vector3 origin = _flashlightLight.transform.position;
        Vector3 toMonster = targetPosition - origin;
        float distance = toMonster.magnitude;

        if (distance <= 0.001f || distance > maxDetectionDistance)
            return false;

        float halfAngle = _flashlightLight.type == LightType.Spot
            ? _flashlightLight.spotAngle * 0.5f
            : 25f;

        float angle = Vector3.Angle(_flashlightLight.transform.forward, toMonster);
        if (angle > halfAngle + extraAnglePadding)
            return false;

        if (Physics.Raycast(origin, toMonster.normalized, out RaycastHit hit, distance, visibilityMask, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(monsterTransform);

        return !requireDirectRaycastHit;
    }

    void CacheMonsterComponents()
    {
        if (monsterTransform == null)
            return;

        _monsterRenderers = monsterTransform.GetComponentsInChildren<Renderer>(true);
        _monsterColliders = monsterTransform.GetComponentsInChildren<Collider>(true);
    }

    void SetMonsterVisible(bool visible)
    {
        if (_monsterRenderers == null || _monsterRenderers.Length == 0)
            CacheMonsterComponents();

        for (int i = 0; i < _monsterRenderers.Length; i++)
            _monsterRenderers[i].enabled = visible;

        for (int i = 0; i < _monsterColliders.Length; i++)
            _monsterColliders[i].enabled = visible;
    }
}
