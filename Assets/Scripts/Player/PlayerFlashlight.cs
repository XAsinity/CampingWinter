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
    AudioSource _toggleAudio;

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
        }
    }

    void Update()
    {
        if (_light == null) return;

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
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
