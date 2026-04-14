using UnityEngine;
using UnityEngine.UI;

public class PlayerSettingsMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerSettings playerSettings;

    [Header("Performance UI")]
    [SerializeField] Toggle vSyncToggle;
    [SerializeField] Slider frameRateSlider;
    [SerializeField] Text frameRateValueText;

    [Header("Quality UI")]
    [SerializeField] Dropdown qualityDropdown;
    [SerializeField] Dropdown shadowDropdown;
    [SerializeField] Toggle motionBlurToggle;

    [Header("Audio UI")]
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] Text masterVolumeValueText;
    [SerializeField] Slider menuMusicVolumeSlider;
    [SerializeField] Text menuMusicVolumeValueText;

    [Header("Controls UI")]
    [SerializeField] Slider mouseSensitivitySlider;
    [SerializeField] Text mouseSensitivityValueText;

    [Header("Buttons")]
    [SerializeField] Button applyButton;
    [SerializeField] Button revertButton;

    bool _isUpdatingUi;

    bool _pendingVSync;
    int _pendingFrameRate;
    int _pendingQualityIndex;
    ShadowQuality _pendingShadowQuality;
    bool _pendingMotionBlur;
    float _pendingMasterVolume;
    float _pendingMenuMusicVolume;
    float _pendingMouseSensitivity;

    void Awake()
    {
        if (playerSettings == null)
            playerSettings = FindFirstObjectByType<PlayerSettings>();

        ConfigureOptions();
        HookEvents();
        PullFromSettings();
        PushPendingToUi();
    }

    void ConfigureOptions()
    {
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
        }

        if (shadowDropdown != null)
        {
            shadowDropdown.ClearOptions();
            shadowDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Disable",
                "HardOnly",
                "All"
            });
        }

        if (frameRateSlider != null)
        {
            frameRateSlider.minValue = 30f;
            frameRateSlider.maxValue = 240f;
            frameRateSlider.wholeNumbers = true;
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 4f;
        }

        if (menuMusicVolumeSlider != null)
        {
            menuMusicVolumeSlider.minValue = 0f;
            menuMusicVolumeSlider.maxValue = 1f;
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 0.2f;
            mouseSensitivitySlider.maxValue = 8f;
        }
    }

    void HookEvents()
    {
        if (vSyncToggle != null)
            vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);

        if (frameRateSlider != null)
            frameRateSlider.onValueChanged.AddListener(OnFrameRateChanged);

        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (shadowDropdown != null)
            shadowDropdown.onValueChanged.AddListener(OnShadowChanged);

        if (motionBlurToggle != null)
            motionBlurToggle.onValueChanged.AddListener(OnMotionBlurChanged);

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (menuMusicVolumeSlider != null)
            menuMusicVolumeSlider.onValueChanged.AddListener(OnMenuMusicVolumeChanged);

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);

        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyPending);

        if (revertButton != null)
            revertButton.onClick.AddListener(RevertFromSaved);
    }

    void PullFromSettings()
    {
        if (playerSettings == null)
            return;

        _pendingVSync = playerSettings.EnableVSync;
        _pendingFrameRate = playerSettings.TargetFrameRate;
        _pendingQualityIndex = playerSettings.QualityLevelIndex;
        _pendingShadowQuality = playerSettings.ShadowQualitySetting;
        _pendingMotionBlur = playerSettings.EnableMotionBlur;
        _pendingMasterVolume = playerSettings.MasterVolume;
        _pendingMenuMusicVolume = playerSettings.MenuMusicVolume;
        _pendingMouseSensitivity = playerSettings.MouseSensitivity;
    }

    void PushPendingToUi()
    {
        _isUpdatingUi = true;

        if (vSyncToggle != null)
            vSyncToggle.isOn = _pendingVSync;

        if (frameRateSlider != null)
            frameRateSlider.value = _pendingFrameRate;

        if (qualityDropdown != null)
            qualityDropdown.value = Mathf.Clamp(_pendingQualityIndex, 0, Mathf.Max(0, qualityDropdown.options.Count - 1));

        if (shadowDropdown != null)
            shadowDropdown.value = ShadowQualityToIndex(_pendingShadowQuality);

        if (motionBlurToggle != null)
            motionBlurToggle.isOn = _pendingMotionBlur;

        if (masterVolumeSlider != null)
            masterVolumeSlider.value = _pendingMasterVolume;

        if (menuMusicVolumeSlider != null)
            menuMusicVolumeSlider.value = _pendingMenuMusicVolume;

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.value = _pendingMouseSensitivity;

        UpdateLabels();
        _isUpdatingUi = false;
    }

    void UpdateLabels()
    {
        if (frameRateValueText != null)
            frameRateValueText.text = $"{_pendingFrameRate} FPS";

        if (masterVolumeValueText != null)
            masterVolumeValueText.text = $"{_pendingMasterVolume:0.00}";

        if (menuMusicVolumeValueText != null)
            menuMusicVolumeValueText.text = $"{_pendingMenuMusicVolume:0.00}";

        if (mouseSensitivityValueText != null)
            mouseSensitivityValueText.text = $"{_pendingMouseSensitivity:0.00}";
    }

    void OnVSyncChanged(bool value)
    {
        if (_isUpdatingUi) return;
        _pendingVSync = value;
    }

    void OnFrameRateChanged(float value)
    {
        if (_isUpdatingUi) return;
        _pendingFrameRate = Mathf.RoundToInt(value);
        UpdateLabels();
    }

    void OnQualityChanged(int index)
    {
        if (_isUpdatingUi) return;
        _pendingQualityIndex = index;
    }

    void OnShadowChanged(int index)
    {
        if (_isUpdatingUi) return;
        _pendingShadowQuality = IndexToShadowQuality(index);
    }

    void OnMotionBlurChanged(bool value)
    {
        if (_isUpdatingUi) return;
        _pendingMotionBlur = value;
    }

    void OnMasterVolumeChanged(float value)
    {
        if (_isUpdatingUi) return;
        _pendingMasterVolume = value;
        UpdateLabels();
    }

    void OnMenuMusicVolumeChanged(float value)
    {
        if (_isUpdatingUi) return;
        _pendingMenuMusicVolume = value;
        UpdateLabels();
    }

    void OnMouseSensitivityChanged(float value)
    {
        if (_isUpdatingUi) return;
        _pendingMouseSensitivity = value;
        UpdateLabels();
    }

    public void ApplyPending()
    {
        if (playerSettings == null)
            return;

        playerSettings.EnableVSync = _pendingVSync;
        playerSettings.TargetFrameRate = _pendingFrameRate;
        playerSettings.QualityLevelIndex = _pendingQualityIndex;
        playerSettings.ShadowQualitySetting = _pendingShadowQuality;
        playerSettings.EnableMotionBlur = _pendingMotionBlur;
        playerSettings.MasterVolume = _pendingMasterVolume;
        playerSettings.MenuMusicVolume = _pendingMenuMusicVolume;
        playerSettings.MouseSensitivity = _pendingMouseSensitivity;
        playerSettings.ApplyAndSave();
    }

    public void RevertFromSaved()
    {
        if (playerSettings == null)
            return;

        playerSettings.ReloadFromDiskAndApply();
        PullFromSettings();
        PushPendingToUi();
    }

    static int ShadowQualityToIndex(ShadowQuality quality)
    {
        switch (quality)
        {
            case ShadowQuality.Disable: return 0;
            case ShadowQuality.HardOnly: return 1;
            default: return 2;
        }
    }

    static ShadowQuality IndexToShadowQuality(int index)
    {
        switch (index)
        {
            case 0: return ShadowQuality.Disable;
            case 1: return ShadowQuality.HardOnly;
            default: return ShadowQuality.All;
        }
    }
}
