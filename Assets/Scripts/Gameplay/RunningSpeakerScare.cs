using UnityEngine;

public class RunningSpeakerScare : NightEvent
{
    [Header("Running Speaker Scare")]
    [SerializeField] AudioSource scareAudioSource;
    [SerializeField] Transform playerOverride;
    [SerializeField] PlayerSleepSystem sleepSystemOverride;
    [SerializeField] bool cancelIfPlayerLeavesBed = true;
    [SerializeField, Min(0f)] float chaseDurationSeconds = 2.5f;
    [SerializeField, Min(0f)] float retreatDurationSeconds = 1.5f;
    [SerializeField, Min(0f)] float chaseSpeed = 4.5f;
    [SerializeField, Min(0f)] float retreatSpeed = 6f;
    [SerializeField, Range(0.1f, 3f)] float playbackPitch = 2.2f;
    [SerializeField] bool disableWhenFinished = true;

    enum ScarePhase
    {
        None,
        Chasing,
        Retreating
    }

    Transform _player;
    PlayerSleepSystem _sleepSystem;
    Vector3 _startPosition;
    float _phaseTimer;
    ScarePhase _phase;

    void Awake()
    {
        _startPosition = transform.position;

        _sleepSystem = sleepSystemOverride;
        if (_sleepSystem == null)
            _sleepSystem = FindFirstObjectByType<PlayerSleepSystem>();

        if (scareAudioSource == null)
            scareAudioSource = GetComponent<AudioSource>();

        if (scareAudioSource != null)
            scareAudioSource.playOnAwake = false;
    }

    void OnEnable()
    {
        if (_startPosition == default)
            _startPosition = transform.position;
    }

    void Update()
    {
        if (_phase == ScarePhase.None)
            return;

        if (cancelIfPlayerLeavesBed && _sleepSystem != null && !_sleepSystem.IsInBag)
        {
            ResetScareState();
            return;
        }

        float dt = Time.deltaTime;
        _phaseTimer += dt;

        if (_phase == ScarePhase.Chasing)
        {
            MoveTowardPlayer(dt);

            if (_phaseTimer >= chaseDurationSeconds)
            {
                _phase = ScarePhase.Retreating;
                _phaseTimer = 0f;
            }

            return;
        }

        MoveBackToStart(dt);

        bool finishedRetreatByTime = _phaseTimer >= retreatDurationSeconds;
        bool reachedStart = Vector3.Distance(transform.position, _startPosition) <= 0.05f;
        if (finishedRetreatByTime || reachedStart)
            EndScare();
    }

    public void TriggerScare()
    {
        StartScare();
    }

    protected override void OnTriggeredByNightSystem()
    {
        StartScare();
    }

    protected override void OnResetByNightSystem()
    {
        ResetScareState();
    }

    void StartScare()
    {
        ResolvePlayerTransform();

        _phase = ScarePhase.Chasing;
        _phaseTimer = 0f;

        if (scareAudioSource != null)
        {
            scareAudioSource.pitch = playbackPitch;
            scareAudioSource.Play();
        }
    }

    void EndScare()
    {
        ResetScareState();

        if (disableWhenFinished)
            gameObject.SetActive(false);
    }

    void ResetScareState()
    {
        _phase = ScarePhase.None;
        _phaseTimer = 0f;
        transform.position = _startPosition;

        if (scareAudioSource != null)
            scareAudioSource.Stop();
    }

    void MoveTowardPlayer(float dt)
    {
        if (_player == null)
            return;

        Vector3 target = _player.position;
        target.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, target, chaseSpeed * dt);
    }

    void MoveBackToStart(float dt)
    {
        transform.position = Vector3.MoveTowards(transform.position, _startPosition, retreatSpeed * dt);
    }

    void ResolvePlayerTransform()
    {
        if (playerOverride != null)
        {
            _player = playerOverride;
            return;
        }

        if (_player != null)
            return;

        var controller = FindFirstObjectByType<FirstPersonController>();
        if (controller != null)
            _player = controller.transform;
    }
}
