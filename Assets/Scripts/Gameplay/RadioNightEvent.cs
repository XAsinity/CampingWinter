using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class RadioNightEvent : NightEvent, IInteractable
{
    [Header("Radio Event")]
    [SerializeField] AudioSource radioSource;
    [SerializeField] AudioClip radioSong;
    [SerializeField, Min(0f)] float songStartTimeSeconds = 0f;
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [SerializeField] string interactionPromptWhenOn = "Turn off radio";
    [SerializeField] string interactionPromptWhenFindHer = "Find Her";

    bool _isRadioOn;
    bool _eventActive;
    bool _hasPlayedSinceTriggered;
    bool _monsterRoundActive;

    public event Action<bool> RadioStarted;
    public event Action<bool> RadioEnded;

    public string InteractionPrompt => _isRadioOn
        ? (_monsterRoundActive ? interactionPromptWhenFindHer : interactionPromptWhenOn)
        : string.Empty;
    public bool IsRadioOn => _isRadioOn;
    public bool IsMonsterRoundActive => _monsterRoundActive;

    void Awake()
    {
        if (radioSource == null)
            radioSource = GetComponent<AudioSource>();

        if (radioSource != null)
            radioSource.playOnAwake = false;
    }

    void Update()
    {
        if (!_eventActive || !_isRadioOn || radioSource == null)
            return;

        if (radioSource.isPlaying)
        {
            _hasPlayedSinceTriggered = true;
            return;
        }

        if (_hasPlayedSinceTriggered && IsClipPlaybackFinished())
            EndEvent();
    }

    public void Interact(GameObject interactor)
    {
        if (_monsterRoundActive)
            return;

        if (_eventActive && _isRadioOn)
            EndEvent();
    }

    public bool TryStartMonsterRound(float maxRoundDurationSeconds)
    {
        if (_isRadioOn || _eventActive)
            return false;

        return StartRadio(monsterRound: true, maxRoundDurationSeconds);
    }

    public void ResolveMonsterObjective()
    {
        if (!_monsterRoundActive || !_isRadioOn)
            return;

        EndEvent();
    }

    protected override void OnTriggeredByNightSystem()
    {
        StartRadio(monsterRound: false, maxRoundDurationSeconds: -1f);
    }

    protected override void OnResetByNightSystem()
    {
        if (radioSource != null)
            radioSource.Stop();

        _isRadioOn = false;
        _eventActive = false;
        _hasPlayedSinceTriggered = false;
        _monsterRoundActive = false;
    }

    bool StartRadio(bool monsterRound, float maxRoundDurationSeconds)
    {
        if (radioSource == null)
            return false;

        if (radioSong != null)
            radioSource.clip = radioSong;

        if (radioSource.clip == null)
            return false;

        float startTime = Mathf.Clamp(songStartTimeSeconds, 0f, Mathf.Max(0f, radioSource.clip.length - 0.01f));
        if (monsterRound && maxRoundDurationSeconds > 0f)
        {
            float maxDurationStart = Mathf.Max(0f, radioSource.clip.length - maxRoundDurationSeconds);
            startTime = Mathf.Max(startTime, maxDurationStart);
        }

        radioSource.loop = false;
        radioSource.volume = volume;
        radioSource.Play();

        if (startTime > 0f)
            radioSource.time = startTime;

        _isRadioOn = true;
        _eventActive = true;
        _hasPlayedSinceTriggered = radioSource.isPlaying;
        _monsterRoundActive = monsterRound;
        RadioStarted?.Invoke(_monsterRoundActive);
        return true;
    }

    void EndEvent()
    {
        if (!_isRadioOn && !_eventActive)
            return;

        bool wasMonsterRound = _monsterRoundActive;

        if (radioSource != null)
            radioSource.Stop();

        _isRadioOn = false;
        _eventActive = false;
        _hasPlayedSinceTriggered = false;
        _monsterRoundActive = false;
        RadioEnded?.Invoke(wasMonsterRound);
    }

    bool IsClipPlaybackFinished()
    {
        if (radioSource == null || radioSource.clip == null)
            return true;

        return radioSource.time >= radioSource.clip.length - 0.05f;
    }
}
