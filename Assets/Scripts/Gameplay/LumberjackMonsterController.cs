using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class LumberjackMonsterController : MonoBehaviour
{
    enum HuntPhase
    {
        Idle,
        Warning,
        ApproachTent,
        Searching,
        Chasing
    }

    [Header("References")]
    [SerializeField] NightSystem nightSystemOverride;
    [SerializeField] FirstPersonController playerControllerOverride;
    [SerializeField] PlayerFlashlight flashlightOverride;
    [SerializeField] Transform monsterTransform;
    [SerializeField] Transform tentTarget;
    [SerializeField] NavMeshAgent navMeshAgentOverride;

    [Header("Activation")]
    [SerializeField] bool activeOnDay2 = true;
    [SerializeField] bool activeOnDay3 = true;
    [SerializeField, Min(0.1f)] float autoTriggerCheckIntervalSeconds = 5f;
    [SerializeField, Range(0f, 1f)] float autoTriggerChancePerCheck = 0.18f;
    [SerializeField, Min(0f)] float minGapBeforeNextTriggerSeconds = 14f;
    [SerializeField, Min(0f)] float maxGapBeforeNextTriggerSeconds = 28f;

    [Header("Spawn")]
    [SerializeField] List<Transform> spawnPoints = new List<Transform>();

    [Header("Warning Phase")]
    [SerializeField, Min(0.1f)] float warningDurationSeconds = 3f;
    [SerializeField] AudioClip warningClip;
    [SerializeField] AudioSource warningAudioSourceOverride;
    [SerializeField, Range(0f, 1f)] float warningVolume = 1f;

    [Header("Search (NavMesh)")]
    [SerializeField, Min(0.1f)] float walkSpeed = 1.35f;
    [SerializeField, Min(0.1f)] float runSpeed = 5.2f;
    [SerializeField, Min(0.1f)] float searchMinSeconds = 5f;
    [SerializeField, Min(0.1f)] float searchMaxSeconds = 15f;
    [SerializeField, Min(0.1f)] float searchRadiusFromTent = 16f;
    [SerializeField, Min(0.1f)] float searchRadiusFromLastKnownNoise = 9f;
    [SerializeField, Min(0.1f)] float repathMinSeconds = 0.8f;
    [SerializeField, Min(0.1f)] float repathMaxSeconds = 2f;
    [SerializeField, Min(0.1f)] float navMeshSampleDistance = 3f;
    [SerializeField, Min(0.1f)] float killDistance = 1.25f;

    [Header("Player Noise Detection")]
    [SerializeField, Min(0f)] float movementNoiseThreshold = 0.15f;
    [SerializeField] bool flashlightOnCountsAsNoise = true;
    [SerializeField, Min(0.1f)] float noiseMemorySeconds = 4f;

    [Header("Monster Audio")]
    [SerializeField] AudioSource movementAudioSource;
    [SerializeField] AudioClip walkLoopClip;
    [SerializeField] AudioClip runLoopClip;
    [SerializeField, Range(0f, 1f)] float walkVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] float runVolume = 0.9f;

    [Header("Events")]
    [SerializeField] UnityEvent onWarningStarted;
    [SerializeField] UnityEvent onWarningFailed;
    [SerializeField] UnityEvent onWarningPassed;
    [SerializeField] UnityEvent onSearchStarted;
    [SerializeField] UnityEvent onChaseStarted;

    [Header("Runtime Debug")]
    [SerializeField] bool liveHuntActive;
    [SerializeField] string livePhase;

    FirstPersonController _playerController;
    CharacterController _playerCharacterController;
    PlayerFlashlight _flashlight;
    NavMeshAgent _agent;

    Renderer[] _renderers;
    Collider[] _colliders;

    HuntPhase _phase;
    float _phaseTimer;
    float _nextAutoTriggerCheckTime;
    float _nextSpawnAllowedTime;
    float _searchDuration;
    float _nextRepathTime;
    float _lastNoiseTime = -999f;
    Vector3 _lastKnownNoisePosition;

    void Awake()
    {
        if (monsterTransform == null)
            monsterTransform = transform;

        _playerController = playerControllerOverride != null ? playerControllerOverride : FindFirstObjectByType<FirstPersonController>();
        _flashlight = flashlightOverride != null ? flashlightOverride : FindFirstObjectByType<PlayerFlashlight>();

        if (_playerController != null)
            _playerCharacterController = _playerController.GetComponent<CharacterController>();

        if (movementAudioSource == null)
            movementAudioSource = GetComponent<AudioSource>();

        _agent = navMeshAgentOverride != null ? navMeshAgentOverride : GetComponent<NavMeshAgent>();

        CacheMonsterComponents();
        SetMonsterVisible(false);
        _phase = HuntPhase.Idle;
    }

    void Update()
    {
        liveHuntActive = _phase != HuntPhase.Idle;
        livePhase = _phase.ToString();

        if (!IsEligibleNight())
        {
            EndHuntCycle();
            return;
        }

        if (_phase == HuntPhase.Idle)
        {
            TryAutoTrigger();
            return;
        }

        _phaseTimer += Time.deltaTime;

        switch (_phase)
        {
            case HuntPhase.Warning:
                UpdateWarningPhase();
                break;
            case HuntPhase.ApproachTent:
                UpdateApproachPhase();
                break;
            case HuntPhase.Searching:
                UpdateSearchPhase();
                break;
            case HuntPhase.Chasing:
                UpdateChasePhase();
                break;
        }
    }

    void TryAutoTrigger()
    {
        if (Time.time < _nextAutoTriggerCheckTime)
            return;

        if (Time.time < _nextSpawnAllowedTime)
            return;

        _nextAutoTriggerCheckTime = Time.time + autoTriggerCheckIntervalSeconds;

        if (Random.value <= Mathf.Clamp01(autoTriggerChancePerCheck))
            TriggerHunt();
    }

    public void TriggerHunt()
    {
        if (_phase != HuntPhase.Idle)
            return;

        if (!IsEligibleNight())
            return;

        if (!TryChooseSpawnPoint(out Transform spawnPoint))
            return;

        WarpOrMoveTo(spawnPoint.position, spawnPoint.rotation);
        SetMonsterVisible(false);
        PlayWarningFromSpawn(spawnPoint);

        _phase = HuntPhase.Warning;
        _phaseTimer = 0f;
        onWarningStarted?.Invoke();

        ScheduleNextSpawnGap();
    }

    public void ResetForNightStart()
    {
        _phase = HuntPhase.Idle;
        _phaseTimer = 0f;
        _nextAutoTriggerCheckTime = 0f;
        _nextSpawnAllowedTime = 0f;
        _searchDuration = 0f;
        _nextRepathTime = 0f;
        _lastNoiseTime = -999f;

        if (_agent != null)
            _agent.ResetPath();

        StopMovementAudio();
        SetMonsterVisible(false);
    }

    void UpdateWarningPhase()
    {
        if (IsPlayerMakingNoise())
        {
            onWarningFailed?.Invoke();
            StartChase();
            return;
        }

        if (_phaseTimer < warningDurationSeconds)
            return;

        onWarningPassed?.Invoke();
        BeginApproachToTent();
    }

    void BeginApproachToTent()
    {
        SetMonsterVisible(true);
        _phase = HuntPhase.ApproachTent;
        _phaseTimer = 0f;
        ApplyAgentSpeed(walkSpeed);
        PlayMovementAudio(walkLoopClip, walkVolume);

        if (tentTarget != null)
            TrySetDestination(tentTarget.position);
    }

    void UpdateApproachPhase()
    {
        if (IsPlayerMakingNoise())
        {
            StartChase();
            return;
        }

        if (tentTarget == null)
        {
            BeginSearchPhase();
            return;
        }

        if (HasReachedDestination())
            BeginSearchPhase();
    }

    void BeginSearchPhase()
    {
        _phase = HuntPhase.Searching;
        _phaseTimer = 0f;
        _searchDuration = Random.Range(Mathf.Max(0.1f, searchMinSeconds), Mathf.Max(searchMinSeconds, searchMaxSeconds));
        _nextRepathTime = 0f;
        onSearchStarted?.Invoke();

        ApplyAgentSpeed(walkSpeed);
        PlayMovementAudio(walkLoopClip, walkVolume);
        PickNextSearchDestination(forceImmediate: true);
    }

    void UpdateSearchPhase()
    {
        if (IsPlayerMakingNoise())
        {
            StartChase();
            return;
        }

        if (_phaseTimer >= _searchDuration)
        {
            EndHuntCycle();
            return;
        }

        if (Time.time >= _nextRepathTime || HasReachedDestination())
            PickNextSearchDestination(forceImmediate: false);
    }

    void StartChase()
    {
        _phase = HuntPhase.Chasing;
        _phaseTimer = 0f;
        _nextRepathTime = 0f;
        onChaseStarted?.Invoke();

        ApplyAgentSpeed(runSpeed);
        PlayMovementAudio(runLoopClip, runVolume);
    }

    void UpdateChasePhase()
    {
        Transform player = ResolvePlayerTransform();
        if (player == null)
            return;

        if (Time.time >= _nextRepathTime)
        {
            TrySetDestination(player.position);
            _nextRepathTime = Time.time + Random.Range(0.08f, 0.2f);
        }

        if (Vector3.Distance(monsterTransform.position, player.position) <= killDistance)
        {
            NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
            if (nightSystem != null)
                nightSystem.HandlePlayerDeath("Lumberjack");

            EndHuntCycle();
        }
    }

    void PickNextSearchDestination(bool forceImmediate)
    {
        Vector3 center = GetSearchCenter();
        float radius = IsRecentNoiseKnown() ? searchRadiusFromLastKnownNoise : searchRadiusFromTent;

        TrySetRandomDestinationAround(center, radius);
        _nextRepathTime = forceImmediate
            ? Time.time + Random.Range(0.5f, 1f)
            : Time.time + Random.Range(Mathf.Max(0.1f, repathMinSeconds), Mathf.Max(repathMinSeconds, repathMaxSeconds));
    }

    Vector3 GetSearchCenter()
    {
        if (IsRecentNoiseKnown())
            return _lastKnownNoisePosition;

        if (tentTarget != null)
            return tentTarget.position;

        return monsterTransform != null ? monsterTransform.position : Vector3.zero;
    }

    bool IsRecentNoiseKnown()
    {
        return Time.time - _lastNoiseTime <= noiseMemorySeconds;
    }

    void EndHuntCycle()
    {
        if (_phase == HuntPhase.Idle)
            return;

        _phase = HuntPhase.Idle;
        _phaseTimer = 0f;
        _searchDuration = 0f;
        _nextRepathTime = 0f;

        if (_agent != null)
            _agent.ResetPath();

        StopMovementAudio();
        SetMonsterVisible(false);
    }

    bool IsPlayerMakingNoise()
    {
        bool madeNoise = false;

        if (flashlightOnCountsAsNoise)
        {
            if (_flashlight == null)
                _flashlight = flashlightOverride != null ? flashlightOverride : FindFirstObjectByType<PlayerFlashlight>();

            if (_flashlight != null && _flashlight.IsOn)
                madeNoise = true;
        }

        if (!madeNoise)
        {
            if (_playerController == null)
                _playerController = playerControllerOverride != null ? playerControllerOverride : FindFirstObjectByType<FirstPersonController>();

            if (_playerController != null && _playerCharacterController == null)
                _playerCharacterController = _playerController.GetComponent<CharacterController>();

            if (_playerCharacterController != null && _playerCharacterController.isGrounded)
                madeNoise = _playerCharacterController.velocity.magnitude >= movementNoiseThreshold;
        }

        if (madeNoise)
        {
            Transform player = ResolvePlayerTransform();
            if (player != null)
                _lastKnownNoisePosition = player.position;

            _lastNoiseTime = Time.time;
        }

        return madeNoise;
    }

    bool TrySetRandomDestinationAround(Vector3 center, float radius)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        float safeRadius = Mathf.Max(0.1f, radius);
        for (int i = 0; i < 10; i++)
        {
            Vector2 random2D = Random.insideUnitCircle * safeRadius;
            Vector3 sample = new Vector3(center.x + random2D.x, center.y, center.z + random2D.y);
            if (!NavMesh.SamplePosition(sample, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            _agent.SetDestination(hit.position);
            return true;
        }

        return TrySetDestination(center);
    }

    bool TrySetDestination(Vector3 target)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
            return true;
        }

        return false;
    }

    bool HasReachedDestination()
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return true;

        if (_agent.pathPending)
            return false;

        return _agent.remainingDistance <= (_agent.stoppingDistance + 0.2f);
    }

    void ApplyAgentSpeed(float speed)
    {
        if (_agent == null)
            return;

        _agent.speed = Mathf.Max(0.1f, speed);
        _agent.angularSpeed = 360f;
        _agent.acceleration = Mathf.Max(_agent.speed * 4f, 8f);
        _agent.stoppingDistance = 0.15f;
    }

    void WarpOrMoveTo(Vector3 position, Quaternion rotation)
    {
        if (monsterTransform == null)
            return;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.Warp(position);
        else
            monsterTransform.position = position;

        monsterTransform.rotation = rotation;
    }

    bool TryChooseSpawnPoint(out Transform spawn)
    {
        spawn = null;
        if (spawnPoints == null || spawnPoints.Count == 0)
            return false;

        var valid = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null)
                valid.Add(spawnPoints[i]);
        }

        if (valid.Count == 0)
            return false;

        spawn = valid[Random.Range(0, valid.Count)];
        return true;
    }

    void PlayWarningFromSpawn(Transform spawnPoint)
    {
        if (warningClip == null)
            return;

        AudioSource source = warningAudioSourceOverride;
        if (source == null && spawnPoint != null)
            source = spawnPoint.GetComponent<AudioSource>();

        if (source == null)
            source = movementAudioSource;

        if (source == null)
            return;

        source.PlayOneShot(warningClip, warningVolume);
    }

    void PlayMovementAudio(AudioClip clip, float volume)
    {
        if (movementAudioSource == null)
            return;

        if (clip == null)
        {
            StopMovementAudio();
            return;
        }

        if (movementAudioSource.clip != clip)
            movementAudioSource.clip = clip;

        movementAudioSource.loop = true;
        movementAudioSource.volume = Mathf.Clamp01(volume);

        if (!movementAudioSource.isPlaying)
            movementAudioSource.Play();
    }

    void StopMovementAudio()
    {
        if (movementAudioSource == null)
            return;

        movementAudioSource.Stop();
        movementAudioSource.clip = null;
    }

    bool IsEligibleNight()
    {
        NightSystem nightSystem = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        if (nightSystem == null)
            return true;

        if (activeOnDay2 && nightSystem.CurrentDay == NightSystem.NightDay.Day2)
            return true;

        if (activeOnDay3 && nightSystem.CurrentDay == NightSystem.NightDay.Day3)
            return true;

        return false;
    }

    Transform ResolvePlayerTransform()
    {
        if (_playerController == null)
            _playerController = playerControllerOverride != null ? playerControllerOverride : FindFirstObjectByType<FirstPersonController>();

        return _playerController != null ? _playerController.transform : null;
    }

    void ScheduleNextSpawnGap()
    {
        float minGap = Mathf.Max(0f, minGapBeforeNextTriggerSeconds);
        float maxGap = Mathf.Max(minGap, maxGapBeforeNextTriggerSeconds);
        _nextSpawnAllowedTime = Time.time + Random.Range(minGap, maxGap);
    }

    void CacheMonsterComponents()
    {
        if (monsterTransform == null)
            return;

        _renderers = monsterTransform.GetComponentsInChildren<Renderer>(true);
        _colliders = monsterTransform.GetComponentsInChildren<Collider>(true);
    }

    void SetMonsterVisible(bool visible)
    {
        if (_renderers == null || _colliders == null)
            CacheMonsterComponents();

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = visible;
            }
        }

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                    _colliders[i].enabled = visible;
            }
        }
    }
}
