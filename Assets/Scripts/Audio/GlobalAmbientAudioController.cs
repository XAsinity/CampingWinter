using System;
using System.Collections.Generic;
using UnityEngine;

public class GlobalAmbientAudioController : MonoBehaviour
{
    public enum OptionalAmbientPlaybackMode
    {
        TwoDInPlayerHead,
        SpatialFromSpeakerSources
    }

    [Serializable]
    public class OptionalAmbientSound
    {
        public string id = "New Optional Sound";
        public AudioClip[] clips;

        [Range(0f, 1f)] public float chancePerCheck = 0.35f;
        [Range(0f, 1f)] public float minVolume = 0.25f;
        [Range(0f, 1f)] public float maxVolume = 0.8f;
        [Range(0.1f, 3f)] public float minPitch = 0.95f;
        [Range(0.1f, 3f)] public float maxPitch = 1.05f;
        public OptionalAmbientPlaybackMode playbackMode = OptionalAmbientPlaybackMode.TwoDInPlayerHead;
        public bool randomizeSpeakerSelection = true;
    }

    [Header("Constant Ambient Loop")]
    [SerializeField] AudioSource constantLoopSource;
    [SerializeField] AudioClip constantLoopClip;
    [SerializeField, Range(0f, 1f)] float constantLoopVolume = 0.6f;
    [SerializeField] bool playLoopOnStart = true;
    [SerializeField] bool useGaplessScheduling = true;
    [SerializeField, Range(0.1f, 5f)] float scheduleLeadTime = 1f;

    [Header("Optional Ambient Events")]
    [SerializeField] List<OptionalAmbientSound> optionalSounds = new List<OptionalAmbientSound>();
    [SerializeField] bool useRandomGlobalCheckInterval = true;
    [SerializeField, Min(0.05f)] float fixedGlobalCheckIntervalSeconds = 15f;
    [SerializeField] Vector2 randomGlobalCheckIntervalRangeSeconds = new Vector2(10f, 25f);
    [SerializeField] bool randomizeInitialCheckTimes = true;
    [SerializeField, Min(0f)] float fixedInitialCheckDelaySeconds = 0f;
    [SerializeField] Vector2 randomInitialCheckDelayRangeSeconds = new Vector2(0f, 15f);

    [Header("Optional Ambient Speaker Sources")]
    [SerializeField] List<AudioSource> optionalSpeakerSources = new List<AudioSource>();

    [Header("Debug")]
    [SerializeField] bool enableAudioDebugLogs;
    [SerializeField, Min(0.1f)] float debugLogCooldownSeconds = 1.5f;

    AudioSource _scheduledLoopSourceB;
    double _nextScheduledLoopTime;
    bool _nextScheduleUsesPrimary;
    bool _isGaplessLoopPlaying;
    float _volumeMultiplier = 1f;
    float _nextOptionalCheckTime;
    float _optionalPlaybackLockedUntil;
    float _nextAllowedDebugLogTime;
    Transform _optionalRuntimeRoot;

    void Awake()
    {
        EnsureLoopSource();
        EnsureSecondaryScheduledSource();
        EnsureOptionalRuntimeRoot();
        ConfigureLoopSource(constantLoopSource, loop: !useGaplessScheduling);
        ConfigureLoopSource(_scheduledLoopSourceB, loop: false);
        ConfigureOptionalSpeakerSources();
    }

    void Start()
    {
        if (playLoopOnStart)
            PlayConstantLoop();

        float now = Time.time;

        if (randomizeInitialCheckTimes)
        {
            float min = Mathf.Max(0f, Mathf.Min(randomInitialCheckDelayRangeSeconds.x, randomInitialCheckDelayRangeSeconds.y));
            float max = Mathf.Max(min, Mathf.Max(randomInitialCheckDelayRangeSeconds.x, randomInitialCheckDelayRangeSeconds.y));
            _nextOptionalCheckTime = now + UnityEngine.Random.Range(min, max);
        }
        else
        {
            _nextOptionalCheckTime = now + Mathf.Max(0f, fixedInitialCheckDelaySeconds);
        }

        LogAudioDebug($"Initial optional check scheduled at t={_nextOptionalCheckTime:0.00}");
    }

    void Update()
    {
        if (useGaplessScheduling)
            UpdateGaplessLoopSchedule();

        float now = Time.time;
        if (now < _nextOptionalCheckTime)
            return;

        _nextOptionalCheckTime = now + GetNextGlobalCheckInterval();
        LogAudioDebug($"Global optional check at t={now:0.00}, next at t={_nextOptionalCheckTime:0.00}");

        if (now < _optionalPlaybackLockedUntil)
        {
            LogAudioDebug($"Optional playback locked until t={_optionalPlaybackLockedUntil:0.00}");
            return;
        }

        TryPlayGlobalOptional(now);
    }

    void TryPlayGlobalOptional(float now)
    {
        var validSounds = new List<OptionalAmbientSound>();
        for (int i = 0; i < optionalSounds.Count; i++)
        {
            var sound = optionalSounds[i];
            if (sound == null) continue;
            if (sound.clips == null || sound.clips.Length == 0)
                continue;

            validSounds.Add(sound);
        }

        if (validSounds.Count == 0)
            return;

        OptionalAmbientSound selectedSound = validSounds[UnityEngine.Random.Range(0, validSounds.Count)];
        if (UnityEngine.Random.value > selectedSound.chancePerCheck)
        {
            LogAudioDebug($"Optional sound '{selectedSound.id}' failed chance roll.");
            return;
        }

        AudioClip clip = selectedSound.clips[UnityEngine.Random.Range(0, selectedSound.clips.Length)];
        if (clip == null)
            return;

        float volume = GetRandomizedVolume(selectedSound);
        float pitch = GetRandomizedPitch(selectedSound);

        _optionalPlaybackLockedUntil = now + (clip.length / Mathf.Max(0.01f, pitch));
        LogAudioDebug($"Playing optional '{selectedSound.id}' clip '{clip.name}' mode={selectedSound.playbackMode}, vol={volume:0.00}, pitch={pitch:0.00}");

        if (selectedSound.playbackMode == OptionalAmbientPlaybackMode.SpatialFromSpeakerSources)
            PlayOptionalClipFromSpeaker(clip, volume, pitch, selectedSound.randomizeSpeakerSelection);
        else
            PlayOptionalClip2D(clip, volume, pitch);
    }

    public void PlayConstantLoop()
    {
        EnsureLoopSource();
        EnsureSecondaryScheduledSource();

        if (constantLoopClip == null)
            return;

        if (constantLoopClip.loadState != AudioDataLoadState.Loaded)
            constantLoopClip.LoadAudioData();

        if (useGaplessScheduling)
        {
            StartGaplessLoop();
            return;
        }

        ConfigureLoopSource(constantLoopSource, loop: true);

        if (constantLoopSource.clip != constantLoopClip)
            constantLoopSource.clip = constantLoopClip;

        if (!constantLoopSource.isPlaying)
            constantLoopSource.Play();

        LogAudioDebug("Constant ambient loop started.");
    }

    public void StopConstantLoop()
    {
        _isGaplessLoopPlaying = false;

        if (constantLoopSource != null && constantLoopSource.isPlaying)
            constantLoopSource.Stop();

        if (_scheduledLoopSourceB != null && _scheduledLoopSourceB.isPlaying)
            _scheduledLoopSourceB.Stop();

        LogAudioDebug("Constant ambient loop stopped.");
    }

    public void SetVolumeMultiplier(float multiplier)
    {
        _volumeMultiplier = Mathf.Clamp01(multiplier);

        if (constantLoopSource != null)
            constantLoopSource.volume = constantLoopVolume * _volumeMultiplier;

        if (_scheduledLoopSourceB != null)
            _scheduledLoopSourceB.volume = constantLoopVolume * _volumeMultiplier;

        for (int i = 0; i < optionalSpeakerSources.Count; i++)
        {
            if (optionalSpeakerSources[i] != null)
                optionalSpeakerSources[i].volume = _volumeMultiplier;
        }
    }

    void EnsureLoopSource()
    {
        if (constantLoopSource == null)
        {
            constantLoopSource = GetComponent<AudioSource>();
            if (constantLoopSource == null)
                constantLoopSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void EnsureOptionalRuntimeRoot()
    {
        if (_optionalRuntimeRoot != null)
            return;

        Transform existing = transform.Find("OptionalAmbientRuntime");
        if (existing != null)
        {
            _optionalRuntimeRoot = existing;
            return;
        }

        var go = new GameObject("OptionalAmbientRuntime");
        go.transform.SetParent(transform, false);
        _optionalRuntimeRoot = go.transform;
    }

    void ConfigureOptionalSpeakerSources()
    {
        for (int i = 0; i < optionalSpeakerSources.Count; i++)
        {
            AudioSource src = optionalSpeakerSources[i];
            if (src == null)
                continue;

            src.playOnAwake = false;
            if (src.spatialBlend < 0.01f)
                src.spatialBlend = 1f;
        }
    }

    void EnsureSecondaryScheduledSource()
    {
        if (_scheduledLoopSourceB != null)
            return;

        Transform existing = transform.Find("AmbientLoopSource_B");
        if (existing != null)
            _scheduledLoopSourceB = existing.GetComponent<AudioSource>();

        if (_scheduledLoopSourceB == null)
        {
            var go = new GameObject("AmbientLoopSource_B");
            go.transform.SetParent(transform, false);
            _scheduledLoopSourceB = go.AddComponent<AudioSource>();
        }
    }

    void ConfigureLoopSource(AudioSource src, bool loop)
    {
        if (src == null) return;

        src.loop = loop;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = constantLoopVolume * _volumeMultiplier;
        src.clip = constantLoopClip;
    }

    void StartGaplessLoop()
    {
        if (constantLoopClip == null) return;

        ConfigureLoopSource(constantLoopSource, loop: false);
        ConfigureLoopSource(_scheduledLoopSourceB, loop: false);

        StopConstantLoop();

        double startTime = AudioSettings.dspTime + 0.1d;
        double length = GetLoopDurationSeconds();

        ScheduleLoopSource(constantLoopSource, startTime);
        ScheduleLoopSource(_scheduledLoopSourceB, startTime + length);

        _nextScheduledLoopTime = startTime + (length * 2d);
        _nextScheduleUsesPrimary = true;
        _isGaplessLoopPlaying = true;
        LogAudioDebug($"Gapless scheduling started. clipLength={length:0.000}s");
    }

    void UpdateGaplessLoopSchedule()
    {
        if (!_isGaplessLoopPlaying || constantLoopClip == null)
            return;

        double dspNow = AudioSettings.dspTime;
        if (dspNow + scheduleLeadTime < _nextScheduledLoopTime)
            return;

        var nextSource = _nextScheduleUsesPrimary ? constantLoopSource : _scheduledLoopSourceB;
        ScheduleLoopSource(nextSource, _nextScheduledLoopTime);

        _nextScheduledLoopTime += GetLoopDurationSeconds();
        _nextScheduleUsesPrimary = !_nextScheduleUsesPrimary;
        LogAudioDebug("Scheduled next ambient loop segment.");
    }

    void ScheduleLoopSource(AudioSource src, double time)
    {
        if (src == null || constantLoopClip == null)
            return;

        src.clip = constantLoopClip;
        src.volume = constantLoopVolume * _volumeMultiplier;
        src.pitch = 1f;
        src.PlayScheduled(time);
    }

    void PlayOptionalClip2D(AudioClip clip, float volume, float pitch)
    {
        EnsureOptionalRuntimeRoot();

        var go = new GameObject("OptionalAmbient_" + clip.name);
        go.transform.SetParent(_optionalRuntimeRoot, false);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume) * _volumeMultiplier;
        src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        src.loop = false;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.Play();

        float lifetime = clip.length / Mathf.Max(0.01f, src.pitch);
        Destroy(go, lifetime + 0.1f);
    }

    void PlayOptionalClipFromSpeaker(AudioClip clip, float volume, float pitch, bool randomizeSpeakerSelection)
    {
        AudioSource speaker = GetSpeakerSource(randomizeSpeakerSelection);
        if (speaker == null)
        {
            PlayOptionalClip2D(clip, volume, pitch);
            return;
        }

        speaker.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        speaker.volume = Mathf.Clamp01(volume) * _volumeMultiplier;
        speaker.PlayOneShot(clip, 1f);
    }

    AudioSource GetSpeakerSource(bool randomizeSelection)
    {
        var candidates = new List<AudioSource>();
        for (int i = 0; i < optionalSpeakerSources.Count; i++)
        {
            AudioSource src = optionalSpeakerSources[i];
            if (src == null || !src.gameObject.activeInHierarchy || !src.enabled)
                continue;

            candidates.Add(src);
        }

        if (candidates.Count == 0)
            return null;

        if (randomizeSelection)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        for (int i = 0; i < candidates.Count; i++)
        {
            if (!candidates[i].isPlaying)
                return candidates[i];
        }

        return candidates[0];
    }

    float GetNextGlobalCheckInterval()
    {
        if (!useRandomGlobalCheckInterval)
            return Mathf.Max(0.05f, fixedGlobalCheckIntervalSeconds);

        float min = Mathf.Max(0.05f, Mathf.Min(randomGlobalCheckIntervalRangeSeconds.x, randomGlobalCheckIntervalRangeSeconds.y));
        float max = Mathf.Max(min, Mathf.Max(randomGlobalCheckIntervalRangeSeconds.x, randomGlobalCheckIntervalRangeSeconds.y));
        return UnityEngine.Random.Range(min, max);
    }

    static float GetRandomizedVolume(OptionalAmbientSound sound)
    {
        float min = Mathf.Clamp01(Mathf.Min(sound.minVolume, sound.maxVolume));
        float max = Mathf.Clamp01(Mathf.Max(sound.minVolume, sound.maxVolume));
        return UnityEngine.Random.Range(min, max);
    }

    static float GetRandomizedPitch(OptionalAmbientSound sound)
    {
        float min = Mathf.Clamp(Mathf.Min(sound.minPitch, sound.maxPitch), 0.1f, 3f);
        float max = Mathf.Clamp(Mathf.Max(sound.minPitch, sound.maxPitch), min, 3f);
        return UnityEngine.Random.Range(min, max);
    }

    double GetLoopDurationSeconds()
    {
        if (constantLoopClip == null || constantLoopClip.frequency <= 0)
            return 0d;

        return (double)constantLoopClip.samples / constantLoopClip.frequency;
    }

    void LogAudioDebug(string message)
    {
        if (!enableAudioDebugLogs)
            return;

        if (Time.unscaledTime < _nextAllowedDebugLogTime)
            return;

        _nextAllowedDebugLogTime = Time.unscaledTime + debugLogCooldownSeconds;
        Debug.Log($"[GlobalAmbientAudioController] {message}", this);
    }
}
