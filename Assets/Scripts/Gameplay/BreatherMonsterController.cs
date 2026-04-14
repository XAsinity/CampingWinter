using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class BreatherMonsterController : MonoBehaviour
{
    enum HuntPhase
    {
        Idle,
        Breathing
    }

    [Header("References")]
    [SerializeField] NightSystem nightSystemOverride;
    [SerializeField] PlayerSleepSystem sleepSystemOverride;
    [SerializeField] PlayerFlashlight flashlightOverride;
    [SerializeField] Transform monsterTransform;
    [SerializeField] Transform detectionTargetOverride;

    [Header("Activation")]
    [Tooltip("Enable Breather logic on Night 2.")]
    [SerializeField] bool activeOnNight2 = true;
    [Tooltip("Enable Breather logic on Night 1 (testing/early content).")]
    [FormerlySerializedAs("forceActiveOnNight1ForTesting")]
    [SerializeField] bool activeOnNight1 = true;
    [Tooltip("How often Breather performs an auto-trigger roll.")]
    [SerializeField, Min(0.1f)] float autoHuntCheckIntervalSeconds = 4f;
    [Tooltip("Base chance per auto-check to start a hunt.")]
    [SerializeField, Range(0f, 1f)] float autoHuntChancePerCheck = 1f;
    [Tooltip("Extra spawn chance added per +1.0 difficulty multiplier above 1.0.")]
    [SerializeField, Range(0f, 1f)] float difficultySpawnChanceBonusPerMultiplier = 0.1f;
    [Tooltip("Cooldown after entering bed before Breather is allowed to trigger.")]
    [SerializeField, Min(0f)] float bedEntrySpawnCooldownSeconds = 3f;
    [Tooltip("Minimum cooldown between Breather spawns.")]
    [SerializeField, Min(0f)] float minGapBeforeNextSpawnSeconds = 4f;
    [Tooltip("Maximum cooldown between Breather spawns.")]
    [SerializeField, Min(0f)] float maxGapBeforeNextSpawnSeconds = 10f;

    [Header("Speakers")]
    [SerializeField] List<AudioSource> speakerSources = new List<AudioSource>();
    [SerializeField] List<AudioClip> breathingClips = new List<AudioClip>();
    [SerializeField, Min(1)] int minBreathCount = 1;
    [SerializeField, Min(1)] int maxBreathCount = 3;
    [SerializeField, Range(0f, 1f)] float minBreathVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float maxBreathVolume = 1f;
    [Tooltip("If enabled, breathing volume gets slightly quieter as difficulty increases.")]
    [SerializeField] bool scaleBreathVolumeByNightDifficulty = true;
    [Tooltip("Volume reduction per +1.0 difficulty multiplier above 1.0.")]
    [SerializeField, Range(0f, 1f)] float difficultyVolumeReductionPerMultiplier = 0.03f;
    [Tooltip("Lowest allowed multiplier when difficulty reduces volume.")]
    [SerializeField, Range(0.2f, 1f)] float minBreathVolumeMultiplier = 0.85f;
    [SerializeField, Range(0.1f, 3f)] float minBreathPitch = 0.95f;
    [SerializeField, Range(0.1f, 3f)] float maxBreathPitch = 1.05f;

    [Header("Monster Placement")]
    [SerializeField] float behindSpeakerDistance = 0.2f;
    [SerializeField] float verticalOffset = 0f;

    [Header("Flashlight Response During Breathing")]
    [SerializeField] bool failIfPlayerLeavesBed = true;
    [SerializeField, Min(0f)] float requiredContinuousSpotSeconds = 0.1f;

    [Header("Flashlight Check")]
    [SerializeField, Min(0.5f)] float maxDetectionDistance = 20f;
    [SerializeField, Min(0f)] float extraAnglePadding = 2f;
    [SerializeField] bool requireDirectRaycastHit = true;
    [SerializeField] LayerMask visibilityMask = ~0;

    [Header("Success Scare")]
    [SerializeField] bool disruptFlashlightOnSuccess = true;
    [SerializeField, Min(0f)] float successForcedOffSeconds = 0.25f;
    [SerializeField, Min(0f)] float successRecoveryFlickerSeconds = 0.9f;
    [SerializeField, Min(0.01f)] float successRecoveryFlickerSpeed = 16f;

    [Header("Audio Hooks")]
    [SerializeField] AudioSource monsterAudioSource;
    [SerializeField] AudioClip spottedClip;
    [SerializeField, Range(0f, 1f)] float spottedVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] float spottedPitch = 1f;

    [Header("Events")]
    [SerializeField] UnityEvent onHuntStarted;
    [SerializeField] UnityEvent onHuntSucceeded;
    [SerializeField] UnityEvent onHuntFailed;

    [Header("Runtime Debug")]
    [SerializeField] bool liveHuntActive;
    [SerializeField] string livePhase;

    PlayerSleepSystem _sleepSystem;
    PlayerFlashlight _flashlight;
    Light _flashlightLight;

    HuntPhase _phase;
    AudioSource _activeSpeaker;
    int _remainingBreathClips;
    float _spotTimer;
    float _nextAutoHuntCheckTime;
    bool _wasInBedLastFrame;
    float _lastBedEnterTime = -9999f;
    float _nextSpawnAllowedTime;

    void Awake()
    {
        if (monsterTransform == null)
            monsterTransform = transform;

        _sleepSystem = sleepSystemOverride;
        if (_sleepSystem == null)
            _sleepSystem = FindFirstObjectByType<PlayerSleepSystem>();

        _flashlight = flashlightOverride;
        if (_flashlight == null)
            _flashlight = FindFirstObjectByType<PlayerFlashlight>();

        if (monsterAudioSource == null)
            monsterAudioSource = GetComponent<AudioSource>();

        SetMonsterVisible(false);
        _phase = HuntPhase.Idle;
        _nextAutoHuntCheckTime = 0f;
    }

    void Update()
    {
        liveHuntActive = _phase != HuntPhase.Idle;
        livePhase = _phase.ToString();

        TrackBedEntryState();

        if (_phase == HuntPhase.Idle)
        {
            TryAutoTriggerHunt();
            return;
        }

        if (failIfPlayerLeavesBed && !IsPlayerValidForHunt())
        {
            FailHunt("Left bed");
            return;
        }

        UpdateBreathingPhase();
    }

    void TrackBedEntryState()
    {
        bool inBed = IsPlayerValidForHunt();
        if (inBed && !_wasInBedLastFrame)
        {
            _lastBedEnterTime = Time.time;
            _nextAutoHuntCheckTime = Mathf.Max(_nextAutoHuntCheckTime, Time.time + autoHuntCheckIntervalSeconds);
        }

        _wasInBedLastFrame = inBed;
    }

    bool IsBreathingAudioActive()
    {
        return _activeSpeaker != null && _activeSpeaker.isPlaying;
    }

    void TryAutoTriggerHunt()
    {
        if (!IsEligibleNight())
            return;

        if (!CanTriggerHuntNow())
            return;

        if (Time.time < _nextAutoHuntCheckTime)
            return;

        if (Time.time < _nextSpawnAllowedTime)
            return;

        _nextAutoHuntCheckTime = Time.time + autoHuntCheckIntervalSeconds;

        float chance = GetScaledSpawnChancePerCheck();
        if (Random.value <= chance)
            TriggerHunt();
    }

    float GetScaledSpawnChancePerCheck()
    {
        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        float difficultyMultiplier = nightSystem != null ? nightSystem.DifficultyMultiplier : 1f;

        float bonus = Mathf.Max(0f, difficultyMultiplier - 1f) * difficultySpawnChanceBonusPerMultiplier;
        return Mathf.Clamp01(autoHuntChancePerCheck + bonus);
    }

    public void TriggerHunt()
    {
        if (_phase != HuntPhase.Idle)
            return;

        if (!IsEligibleNight())
            return;

        if (!CanTriggerHuntNow())
            return;

        if (Time.time < _nextSpawnAllowedTime)
            return;

        if (!TryChooseSpeaker(out _activeSpeaker))
            return;

        if (breathingClips == null || breathingClips.Count == 0)
            return;

        PlaceMonsterBehindSpeaker(_activeSpeaker.transform);
        SetMonsterVisible(true);

        _remainingBreathClips = Random.Range(Mathf.Max(1, minBreathCount), Mathf.Max(minBreathCount, maxBreathCount) + 1);
        _phase = HuntPhase.Breathing;
        _spotTimer = 0f;

        ScheduleNextSpawnGap();
        PlayNextBreathClip();
        onHuntStarted?.Invoke();
    }

    bool CanTriggerHuntNow()
    {
        if (!IsPlayerValidForHunt())
            return false;

        if (Time.time - _lastBedEnterTime < bedEntrySpawnCooldownSeconds)
            return false;

        if (_flashlight == null)
            _flashlight = flashlightOverride != null ? flashlightOverride : FindFirstObjectByType<PlayerFlashlight>();

        return _flashlight != null && !_flashlight.IsOn;
    }

    public void ResetForNightStart()
    {
        _phase = HuntPhase.Idle;
        _activeSpeaker = null;
        _remainingBreathClips = 0;
        _spotTimer = 0f;
        _nextAutoHuntCheckTime = 0f;
        _wasInBedLastFrame = false;
        _lastBedEnterTime = -9999f;
        _nextSpawnAllowedTime = 0f;
        SetMonsterVisible(false);
    }

    void UpdateBreathingPhase()
    {
        if (_activeSpeaker == null)
        {
            FailHunt("No speaker");
            return;
        }

        if (IsBreathingAudioActive())
        {
            if (IsMonsterInFlashlightView())
            {
                _spotTimer += Time.deltaTime;
                if (_spotTimer >= requiredContinuousSpotSeconds)
                {
                    SucceedHunt();
                    return;
                }
            }
            else
            {
                _spotTimer = 0f;
            }

            return;
        }

        if (_remainingBreathClips > 0)
        {
            PlayNextBreathClip();
            return;
        }

        FailHunt("Missed breathing window");
    }

    void SucceedHunt()
    {
        if (_activeSpeaker != null && _activeSpeaker.isPlaying)
            _activeSpeaker.Stop();

        PlayHookClip(spottedClip, spottedVolume, spottedPitch);

        if (_flashlight == null)
            _flashlight = flashlightOverride != null ? flashlightOverride : FindFirstObjectByType<PlayerFlashlight>();

        if (disruptFlashlightOnSuccess && _flashlight != null)
            _flashlight.TriggerScareDisruption(successForcedOffSeconds, successRecoveryFlickerSeconds, successRecoveryFlickerSpeed);

        onHuntSucceeded?.Invoke();
        EndCurrentHuntCycle();
    }

    void PlayHookClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
            return;

        AudioSource source = monsterAudioSource != null ? monsterAudioSource : _activeSpeaker;
        if (source == null)
            return;

        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }

    void FailHunt(string reason)
    {
        onHuntFailed?.Invoke();

        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        if (nightSystem != null)
            nightSystem.HandlePlayerDeath("Breather");

        EndCurrentHuntCycle();
    }

    void EndCurrentHuntCycle()
    {
        if (_activeSpeaker != null && _activeSpeaker.isPlaying)
            _activeSpeaker.Stop();

        _phase = HuntPhase.Idle;
        _activeSpeaker = null;
        _remainingBreathClips = 0;
        _spotTimer = 0f;
        SetMonsterVisible(false);

        _nextAutoHuntCheckTime = Mathf.Max(_nextAutoHuntCheckTime, Time.time + autoHuntCheckIntervalSeconds);
    }

    void ScheduleNextSpawnGap()
    {
        float minGap = Mathf.Max(0f, minGapBeforeNextSpawnSeconds);
        float maxGap = Mathf.Max(minGap, maxGapBeforeNextSpawnSeconds);
        _nextSpawnAllowedTime = Time.time + Random.Range(minGap, maxGap);
    }

    void PlayNextBreathClip()
    {
        if (_activeSpeaker == null)
            return;

        var validClips = new List<AudioClip>();
        for (int i = 0; i < breathingClips.Count; i++)
        {
            if (breathingClips[i] != null)
                validClips.Add(breathingClips[i]);
        }

        if (validClips.Count == 0)
        {
            _remainingBreathClips = 0;
            return;
        }

        AudioClip clip = validClips[Random.Range(0, validClips.Count)];
        _activeSpeaker.pitch = Random.Range(minBreathPitch, maxBreathPitch);

        Vector2 volumeRange = GetScaledBreathVolumeRange();
        _activeSpeaker.PlayOneShot(clip, Random.Range(volumeRange.x, volumeRange.y));

        _remainingBreathClips = Mathf.Max(0, _remainingBreathClips - 1);
    }

    Vector2 GetScaledBreathVolumeRange()
    {
        float minVol = minBreathVolume;
        float maxVol = maxBreathVolume;

        if (scaleBreathVolumeByNightDifficulty)
        {
            NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
            float difficultyMultiplier = nightSystem != null ? nightSystem.DifficultyMultiplier : 1f;

            float reduction = Mathf.Max(0f, difficultyMultiplier - 1f) * difficultyVolumeReductionPerMultiplier;
            float multiplier = Mathf.Clamp(1f - reduction, minBreathVolumeMultiplier, 1f);

            minVol *= multiplier;
            maxVol *= multiplier;
        }

        minVol = Mathf.Clamp01(minVol);
        maxVol = Mathf.Clamp(maxVol, minVol, 1f);
        return new Vector2(minVol, maxVol);
    }

    bool TryChooseSpeaker(out AudioSource speaker)
    {
        speaker = null;
        var valid = new List<AudioSource>();
        for (int i = 0; i < speakerSources.Count; i++)
        {
            if (speakerSources[i] != null)
                valid.Add(speakerSources[i]);
        }

        if (valid.Count == 0)
            return false;

        speaker = valid[Random.Range(0, valid.Count)];
        return true;
    }

    void PlaceMonsterBehindSpeaker(Transform speakerTransform)
    {
        if (monsterTransform == null || speakerTransform == null)
            return;

        Vector3 position = speakerTransform.position - speakerTransform.forward * behindSpeakerDistance;
        position.y += verticalOffset;
        monsterTransform.position = position;
    }

    bool IsEligibleNight()
    {
        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        if (nightSystem == null)
            return true;

        if (activeOnNight2 && nightSystem.CurrentDay == NightSystem.NightDay.Day2)
            return true;

        if (activeOnNight1 && nightSystem.CurrentDay == NightSystem.NightDay.Day1)
            return true;

        return false;
    }

    bool IsPlayerValidForHunt()
    {
        if (_sleepSystem == null)
            _sleepSystem = sleepSystemOverride != null ? sleepSystemOverride : FindFirstObjectByType<PlayerSleepSystem>();

        return _sleepSystem != null && _sleepSystem.IsInBag;
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
            return IsHitOnBreatherHierarchy(hit.transform);

        return !requireDirectRaycastHit;
    }

    bool IsHitOnBreatherHierarchy(Transform hitTransform)
    {
        if (hitTransform == null || monsterTransform == null)
            return false;

        if (hitTransform == monsterTransform)
            return true;

        if (hitTransform.IsChildOf(monsterTransform))
            return true;

        if (monsterTransform.IsChildOf(hitTransform))
            return true;

        if (detectionTargetOverride == null)
            return false;

        if (hitTransform == detectionTargetOverride)
            return true;

        if (hitTransform.IsChildOf(detectionTargetOverride))
            return true;

        if (detectionTargetOverride.IsChildOf(hitTransform))
            return true;

        return false;
    }

    void SetMonsterVisible(bool visible)
    {
        if (monsterTransform == null)
            return;

        var renderers = monsterTransform.GetComponentsInChildren<Renderer>(true);
        var colliders = monsterTransform.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = visible;
    }
}
