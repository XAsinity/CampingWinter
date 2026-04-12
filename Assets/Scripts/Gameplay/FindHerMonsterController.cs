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
    [SerializeField, Min(0f)] float minGapBeforeNextSpawnSeconds = 4f;
    [SerializeField, Min(0f)] float maxGapBeforeNextSpawnSeconds = 10f;

    [Header("Find-Her Time Pressure")]
    [SerializeField, Min(1f)] float baseFindHerTimeSeconds = 18f;
    [SerializeField, Range(0f, 1f)] float difficultyTimeReductionPerMultiplier = 0.42f;
    [SerializeField, Range(0f, 1f)] float agitationTimeReductionPerStack = 0.025f;
    [SerializeField, Range(0.05f, 1f)] float minFindHerTimeMultiplier = 0.25f;

    [Header("Monster Spawns")]
    [SerializeField] List<Transform> spawnPoints = new List<Transform>();

    [Header("Facing")]
    [SerializeField] bool facePlayerWhileHunting = true;
    [SerializeField] Transform modelFacingRootOverride;
    [SerializeField] float modelFacingYawOffsetDegrees = 0f;
    [SerializeField, Min(0f)] float modelFacingTurnSpeed = 20f;

    [Header("Flashlight Detection")]
    [SerializeField, Min(0.5f)] float maxDetectionDistance = 20f;
    [SerializeField, Min(0f)] float extraAnglePadding = 2f;
    [SerializeField, Min(0f)] float requiredContinuousSpotSeconds = 0.15f;
    [SerializeField] bool requireDirectRaycastHit = true;
    [SerializeField] LayerMask visibilityMask = ~0;

    [Header("Found Scare Response")]
    [SerializeField] bool disruptFlashlightOnFound = true;
    [SerializeField, Min(0f)] float foundForcedOffSeconds = 0.35f;
    [SerializeField, Min(0f)] float foundRecoveryFlickerSeconds = 1.2f;
    [SerializeField, Min(0.01f)] float foundRecoveryFlickerSpeed = 18f;

    [Header("Audio Hooks")]
    [SerializeField] AudioSource monsterAudioSource;
    [SerializeField] AudioClip spottedClip;
    [SerializeField, Range(0f, 1f)] float spottedVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] float spottedPitch = 1f;
    [SerializeField] AudioClip disappearClip;
    [SerializeField, Range(0f, 1f)] float disappearVolume = 0.9f;
    [SerializeField, Range(0.1f, 3f)] float disappearPitch = 1f;

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
    float _nextSpawnAllowedTime;

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

        if (monsterAudioSource == null)
            monsterAudioSource = GetComponent<AudioSource>();

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

        UpdateMonsterFacing();

        if (IsMonsterInFlashlightView())
        {
            _spotTimer += Time.deltaTime;
            if (_spotTimer >= requiredContinuousSpotSeconds)
            {
                if (_flashlight == null)
                    _flashlight = flashlightOverride != null ? flashlightOverride : FindFirstObjectByType<PlayerFlashlight>();

                if (disruptFlashlightOnFound && _flashlight != null)
                    _flashlight.TriggerScareDisruption(foundForcedOffSeconds, foundRecoveryFlickerSeconds, foundRecoveryFlickerSpeed);

                PlayHookClip(spottedClip, spottedVolume, spottedPitch);

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

        if (Time.time < _nextSpawnAllowedTime)
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

        if (!TryMoveMonsterToRandomSpawn())
            return;

        float timeLimitSeconds = GetCurrentFindHerTimeLimitSeconds();

        if (!radioEvent.TryStartMonsterRound(timeLimitSeconds))
            return;

        SetMonsterVisible(true);
        _huntActive = true;
        _resolvedCurrentRound = false;
        liveCurrentFindHerTime = timeLimitSeconds;
        UpdateMonsterFacing();
    }

    bool TryMoveMonsterToRandomSpawn()
    {
        var validSpawns = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null)
                validSpawns.Add(spawnPoints[i]);
        }

        if (validSpawns.Count == 0)
        {
            Debug.LogWarning("[FindHer] No valid spawn points assigned. Hunt start aborted.");
            return false;
        }

        const float samePositionThreshold = 0.05f;
        if (validSpawns.Count > 1)
        {
            for (int i = validSpawns.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(validSpawns[i].position, transform.position) <= samePositionThreshold)
                    validSpawns.RemoveAt(i);
            }

            if (validSpawns.Count == 0)
            {
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    if (spawnPoints[i] != null)
                        validSpawns.Add(spawnPoints[i]);
                }
            }
        }

        Transform spawn = validSpawns[Random.Range(0, validSpawns.Count)];
        transform.position = spawn.position;
        transform.rotation = spawn.rotation;
        return true;
    }

    void EndHunt()
    {
        bool wasActive = _huntActive;

        _huntActive = false;
        _spotTimer = 0f;
        SetMonsterVisible(false);

        if (wasActive)
            ScheduleNextSpawnGap();
    }

    void ScheduleNextSpawnGap()
    {
        float minGap = Mathf.Max(0f, minGapBeforeNextSpawnSeconds);
        float maxGap = Mathf.Max(minGap, maxGapBeforeNextSpawnSeconds);
        _nextSpawnAllowedTime = Time.time + Random.Range(minGap, maxGap);
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

    void UpdateMonsterFacing()
    {
        if (!facePlayerWhileHunting)
            return;

        Transform facingRoot = modelFacingRootOverride != null ? modelFacingRootOverride : monsterTransform;
        if (facingRoot == null)
            return;

        Transform target = ResolveFacingTarget();
        if (target == null)
            return;

        Vector3 toTarget = target.position - facingRoot.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up)
                             * Quaternion.Euler(0f, modelFacingYawOffsetDegrees, 0f);

        if (modelFacingTurnSpeed <= 0f)
            facingRoot.rotation = desired;
        else
            facingRoot.rotation = Quaternion.Slerp(facingRoot.rotation, desired, Time.deltaTime * modelFacingTurnSpeed);
    }

    Transform ResolveFacingTarget()
    {
        if (_flashlight != null && _flashlight.FlashlightLight != null)
            return _flashlight.FlashlightLight.transform;

        var camera = Camera.main;
        if (camera != null)
            return camera.transform;

        return _sleepSystem != null ? _sleepSystem.transform : null;
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
            return IsHitOnMonsterHierarchy(hit.transform);

        return !requireDirectRaycastHit;
    }

    bool IsHitOnMonsterHierarchy(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        if (hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform))
            return true;

        if (monsterTransform != null)
        {
            if (hitTransform == monsterTransform || hitTransform.IsChildOf(monsterTransform) || monsterTransform.IsChildOf(hitTransform))
                return true;
        }

        if (detectionTargetOverride != null)
        {
            if (hitTransform == detectionTargetOverride || hitTransform.IsChildOf(detectionTargetOverride) || detectionTargetOverride.IsChildOf(hitTransform))
                return true;
        }

        return false;
    }

    void CacheMonsterComponents()
    {
        _monsterRenderers = transform.GetComponentsInChildren<Renderer>(true);
        _monsterColliders = transform.GetComponentsInChildren<Collider>(true);
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

    void PlayHookClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
            return;

        if (monsterAudioSource == null)
        {
            monsterAudioSource = GetComponent<AudioSource>();
            if (monsterAudioSource == null)
                return;
        }

        float originalPitch = monsterAudioSource.pitch;
        monsterAudioSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        monsterAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        monsterAudioSource.pitch = originalPitch;
    }

    public void ResetForNightStart()
    {
        _isArmed = false;
        _huntActive = false;
        _agitation = 0;
        _closedEyesTimer = 0f;
        _spawnRollTimer = 0f;
        _spotTimer = 0f;
        _resolvedCurrentRound = false;
        _nextSpawnAllowedTime = 0f;

        SetMonsterVisible(false);
    }
}
