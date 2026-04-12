using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerFlashlight : MonoBehaviour
{
    [Header("Flashlight")]
    [SerializeField] Light flashlight;
    [SerializeField] AudioSource toggleAudioSource;
    [SerializeField] AudioClip turnOnClip;
    [SerializeField] AudioClip turnOffClip;
    [SerializeField] Key toggleKey = Key.F;
    [SerializeField] bool startEnabled = false;
    [SerializeField] float maxBattery  = 100f;
    [SerializeField] float drainRate   = 1.5f;
    [SerializeField] bool forceNoShadows = true;

    [Header("Night Difficulty Scaling")]
    [SerializeField] bool useNightSystemMultiplier = true;
    [SerializeField] NightSystem nightSystemOverride;
    [SerializeField, Min(0f)] float localDrainMultiplier = 1f;

    [Header("Flicker")]
    [SerializeField] float flickerThreshold = 15f;
    [SerializeField] float flickerSpeed     = 12f;

    Light _light;
    float _battery;
    float _baseIntensity;
    bool  _isOn = true;
    bool _toggleInputEnabled = true;
    AudioSource _toggleAudio;
    Coroutine _disruptionRoutine;
    bool _isDisrupted;

    public float BatteryPercent => (_battery / maxBattery) * 100f;
    public bool  IsOn           => _isOn;
    public Light FlashlightLight => _light;

    void Start()
    {
        _light = flashlight;
        if (_light == null)
        {
            var explicitChild = transform.Find("PlayerCamera/Flashlight");
            if (explicitChild != null)
                _light = explicitChild.GetComponent<Light>();
        }

        if (_light == null)
            _light = GetComponentInChildren<Light>();

        _toggleAudio = toggleAudioSource;
        if (_toggleAudio == null && _light != null)
            _toggleAudio = _light.GetComponent<AudioSource>();
        if (_toggleAudio == null)
            _toggleAudio = GetComponentInChildren<AudioSource>();

        if (_light != null)
        {
            if (forceNoShadows)
                _light.shadows = LightShadows.None;

            _baseIntensity = _light.intensity;
            _battery = maxBattery;
            SetFlashlightEnabled(startEnabled && _battery > 0f);
        }
    }

    void Update()
    {
        if (_light == null) return;

        if (_isDisrupted)
            return;

        if (_toggleInputEnabled && Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            bool nextState = !_isOn && _battery > 0f;
            if (_toggleAudio != null)
            {
                AudioClip clip = nextState ? turnOnClip : turnOffClip;
                if (clip != null)
                    _toggleAudio.PlayOneShot(clip);
            }

            _isOn = nextState;
            _light.enabled = _isOn;
        }

        if (_isOn)
        {
            float effectiveDrain = drainRate * GetBatteryDrainMultiplier();
            _battery -= effectiveDrain * Time.deltaTime;
            _battery = Mathf.Max(0f, _battery);

            if (_battery <= 0f)
            {
                _isOn = false;
                _light.enabled = false;
                return;
            }

            if (BatteryPercent < flickerThreshold)
            {
                float flicker = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
                _light.intensity = _baseIntensity * flicker;
            }
            else
            {
                _light.intensity = _baseIntensity;
            }
        }
    }

    public void SetToggleInputEnabled(bool enabled)
    {
        _toggleInputEnabled = enabled;
    }

    public void SetFlashlightEnabled(bool enabled)
    {
        if (_light == null)
            return;

        _isOn = enabled && _battery > 0f;
        _light.enabled = _isOn;
        _light.intensity = _baseIntensity;
    }

    public void TriggerScareDisruption(float forcedOffSeconds, float recoveryFlickerSeconds, float recoveryFlickerSpeed)
    {
        if (_light == null)
            return;

        if (_disruptionRoutine != null)
            StopCoroutine(_disruptionRoutine);

        _disruptionRoutine = StartCoroutine(ScareDisruptionRoutine(forcedOffSeconds, recoveryFlickerSeconds, recoveryFlickerSpeed));
    }

    IEnumerator ScareDisruptionRoutine(float forcedOffSeconds, float recoveryFlickerSeconds, float recoveryFlickerSpeed)
    {
        _isDisrupted = true;

        _isOn = false;
        _light.enabled = false;
        _light.intensity = _baseIntensity;

        float offT = 0f;
        while (offT < forcedOffSeconds)
        {
            offT += Time.deltaTime;
            yield return null;
        }

        float t = 0f;
        while (t < recoveryFlickerSeconds)
        {
            t += Time.deltaTime;
            float p = recoveryFlickerSeconds <= 0f ? 1f : Mathf.Clamp01(t / recoveryFlickerSeconds);

            float noise = Mathf.PerlinNoise(Time.time * Mathf.Max(0.01f, recoveryFlickerSpeed), 0.173f);
            float onChance = Mathf.Lerp(0.1f, 0.95f, p);
            bool onThisFrame = noise < onChance && _battery > 0f;

            _light.enabled = onThisFrame;
            if (onThisFrame)
            {
                float intensityMul = Mathf.Lerp(0.2f, 1f, p) * (0.35f + 0.65f * noise);
                _light.intensity = _baseIntensity * intensityMul;
            }

            yield return null;
        }

        _isOn = _battery > 0f;
        _light.enabled = _isOn;
        _light.intensity = _baseIntensity;

        _isDisrupted = false;
        _disruptionRoutine = null;
    }

    public void AddBattery(float amount)
    {
        _battery = Mathf.Min(maxBattery, _battery + amount);
    }

    float GetBatteryDrainMultiplier()
    {
        float multiplier = Mathf.Max(0f, localDrainMultiplier);
        if (!useNightSystemMultiplier)
            return multiplier;

        NightSystem system = nightSystemOverride != null ? nightSystemOverride : NightSystem.Instance;
        if (system != null)
            multiplier *= system.FlashlightDrainMultiplier;

        return multiplier;
    }
}
