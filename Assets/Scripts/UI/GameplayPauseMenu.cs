using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameplayPauseMenu : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] Key togglePauseKey = Key.Escape;
    [SerializeField] bool pauseTimeScale = true;

    [Header("Flow")]
    [SerializeField] bool loadMainMenuSceneOnQuit = true;
    [SerializeField] string mainMenuSceneName = "MainMenu";
    [SerializeField] bool pauseAllAudio = true;

    [Header("UI Text")]
    [SerializeField] string pauseTitle = "Paused";
    [SerializeField] string resumeText = "Resume";
    [SerializeField] string settingsText = "Settings";
    [SerializeField] string quitText = "Quit To Menu";
    [SerializeField] string backText = "Back";

    [Header("UI Style")]
    [SerializeField] Font uiFontOverride;
    [SerializeField] Color panelTint = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] Color primaryButtonTint = new Color(0.2f, 0.4f, 0.26f, 0.95f);
    [SerializeField] Color secondaryButtonTint = new Color(0.16f, 0.23f, 0.35f, 0.95f);
    [SerializeField] Color titleTextColor = new Color(0.86f, 0.94f, 1f, 1f);
    [SerializeField] Color bodyTextColor = new Color(0.92f, 0.96f, 1f, 0.95f);

    [Header("UI Image Overrides")]
    [SerializeField] Sprite panelSprite;
    [SerializeField] Sprite primaryButtonSprite;
    [SerializeField] Sprite secondaryButtonSprite;
    [SerializeField] Sprite fieldBackgroundSprite;
    [SerializeField] Sprite sliderFillSprite;

    GameObject _canvasObject;
    GameObject _mainPanel;
    GameObject _settingsPanel;

    Toggle _vSyncToggle;
    Toggle _motionBlurToggle;
    Slider _fpsSlider;
    Text _fpsValue;
    Slider _masterVolumeSlider;
    Text _masterVolumeValue;
    Slider _menuMusicVolumeSlider;
    Text _menuMusicVolumeValue;
    Slider _mouseSensitivitySlider;
    Text _mouseSensitivityValue;

    FirstPersonController _controller;
    PlayerInteraction _interaction;
    PlayerFlashlight _flashlight;
    PlayerSleepSystem _sleepSystem;

    bool _isOpen;
    CursorLockMode _previousCursorLock;
    bool _previousCursorVisible;
    bool _previousAudioListenerPause;
    readonly List<Behaviour> _frozenBehaviours = new List<Behaviour>();
    readonly List<bool> _frozenBehaviourStates = new List<bool>();

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!keyboard[togglePauseKey].wasPressedThisFrame)
            return;

        if (_isOpen)
            ClosePauseMenu();
        else
            OpenPauseMenu();
    }

    void OpenPauseMenu()
    {
        if (_isOpen)
            return;

        EnsureEventSystem();
        BuildUiIfNeeded();

        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;

        if (_mainPanel != null)
            _mainPanel.SetActive(true);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        RefreshSettingsUi();

        CacheAndFreezeRuntimeSystems();

        if (pauseTimeScale)
            Time.timeScale = 0f;

        if (pauseAllAudio)
        {
            _previousAudioListenerPause = AudioListener.pause;
            AudioListener.pause = true;
        }

        SetGameplayInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _isOpen = true;
    }

    void ClosePauseMenu()
    {
        if (!_isOpen)
            return;

        if (_canvasObject != null)
            _canvasObject.SetActive(false);

        if (pauseTimeScale)
            Time.timeScale = 1f;

        if (pauseAllAudio)
            AudioListener.pause = _previousAudioListenerPause;

        RestoreFrozenRuntimeSystems();

        SetGameplayInputEnabled(true);

        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        _isOpen = false;
    }

    void CacheAndFreezeRuntimeSystems()
    {
        _frozenBehaviours.Clear();
        _frozenBehaviourStates.Clear();

        AddAndFreeze(FindFirstObjectByType<NightSystem>());

        var findHerMonsters = FindObjectsByType<FindHerMonsterController>(FindObjectsSortMode.None);
        for (int i = 0; i < findHerMonsters.Length; i++)
            AddAndFreeze(findHerMonsters[i]);

        var breatherMonsters = FindObjectsByType<BreatherMonsterController>(FindObjectsSortMode.None);
        for (int i = 0; i < breatherMonsters.Length; i++)
            AddAndFreeze(breatherMonsters[i]);

        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name == "LumberjackMonsterController")
                AddAndFreeze(behaviour);
        }

        var radios = FindObjectsByType<RadioNightEvent>(FindObjectsSortMode.None);
        for (int i = 0; i < radios.Length; i++)
            AddAndFreeze(radios[i]);

        var runningScares = FindObjectsByType<RunningSpeakerScare>(FindObjectsSortMode.None);
        for (int i = 0; i < runningScares.Length; i++)
            AddAndFreeze(runningScares[i]);
    }

    void RestoreFrozenRuntimeSystems()
    {
        int count = Mathf.Min(_frozenBehaviours.Count, _frozenBehaviourStates.Count);
        for (int i = 0; i < count; i++)
        {
            Behaviour behaviour = _frozenBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = _frozenBehaviourStates[i];
        }

        _frozenBehaviours.Clear();
        _frozenBehaviourStates.Clear();
    }

    void AddAndFreeze(Behaviour behaviour)
    {
        if (behaviour == null)
            return;

        _frozenBehaviours.Add(behaviour);
        _frozenBehaviourStates.Add(behaviour.enabled);
        behaviour.enabled = false;
    }

    void BuildUiIfNeeded()
    {
        if (_canvasObject != null)
        {
            _canvasObject.SetActive(true);
            return;
        }

        _canvasObject = new GameObject("GameplayPauseMenuCanvas");
        var canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;
        _canvasObject.AddComponent<CanvasScaler>();
        _canvasObject.AddComponent<GraphicRaycaster>();

        _mainPanel = CreatePanel("MainPanel", _canvasObject.transform, panelSprite, panelTint);
        _settingsPanel = CreatePanel("SettingsPanel", _canvasObject.transform, panelSprite, panelTint);

        BuildMainPanel();
        BuildSettingsPanel();
    }

    void BuildMainPanel()
    {
        Font font = GetFont();

        CreateLabel("PauseTitle", _mainPanel.transform, pauseTitle, 48, titleTextColor, new Vector2(0.5f, 0.68f), new Vector2(700f, 90f));

        CreateButton("ResumeButton", _mainPanel.transform, resumeText, primaryButtonSprite, primaryButtonTint,
            new Vector2(0.5f, 0.52f), new Vector2(260f, 62f), ClosePauseMenu, font);

        CreateButton("SettingsButton", _mainPanel.transform, settingsText, secondaryButtonSprite, secondaryButtonTint,
            new Vector2(0.5f, 0.43f), new Vector2(260f, 56f), ShowSettingsPanel, font);

        CreateButton("QuitButton", _mainPanel.transform, quitText, secondaryButtonSprite, secondaryButtonTint,
            new Vector2(0.5f, 0.34f), new Vector2(260f, 56f), QuitToMainMenu, font);
    }

    void BuildSettingsPanel()
    {
        Font font = GetFont();

        CreateLabel("SettingsTitle", _settingsPanel.transform, "Settings", 42, titleTextColor, new Vector2(0.5f, 0.72f), new Vector2(700f, 80f));
        _vSyncToggle = CreateToggleRow(_settingsPanel.transform, "VSync", new Vector2(0.5f, 0.60f));
        _motionBlurToggle = CreateToggleRow(_settingsPanel.transform, "Motion Blur", new Vector2(0.5f, 0.52f));
        _fpsSlider = CreateSliderRow(_settingsPanel.transform, "Frame Rate", new Vector2(0.5f, 0.44f), out _fpsValue, 30f, 240f, true);
        _masterVolumeSlider = CreateSliderRow(_settingsPanel.transform, "Master Volume", new Vector2(0.5f, 0.36f), out _masterVolumeValue, 0f, 4f, false);
        _menuMusicVolumeSlider = CreateSliderRow(_settingsPanel.transform, "Menu Music Volume", new Vector2(0.5f, 0.28f), out _menuMusicVolumeValue, 0f, 1f, false);
        _mouseSensitivitySlider = CreateSliderRow(_settingsPanel.transform, "Mouse Sensitivity", new Vector2(0.5f, 0.20f), out _mouseSensitivityValue, 0.2f, 8f, false);

        CreateButton("ApplySettingsButton", _settingsPanel.transform, "Apply", primaryButtonSprite, primaryButtonTint,
            new Vector2(0.42f, 0.12f), new Vector2(180f, 52f), ApplySettings, font);

        CreateButton("BackButton", _settingsPanel.transform, backText, secondaryButtonSprite, secondaryButtonTint,
            new Vector2(0.58f, 0.12f), new Vector2(180f, 52f), ShowMainPanel, font);

        _fpsSlider.onValueChanged.AddListener(_ =>
        {
            if (_fpsValue != null)
                _fpsValue.text = $"{Mathf.RoundToInt(_fpsSlider.value)} FPS";
        });

        _masterVolumeSlider.onValueChanged.AddListener(_ =>
        {
            if (_masterVolumeValue != null)
                _masterVolumeValue.text = _masterVolumeSlider.value.ToString("0.00");
        });

        _menuMusicVolumeSlider.onValueChanged.AddListener(_ =>
        {
            if (_menuMusicVolumeValue != null)
                _menuMusicVolumeValue.text = _menuMusicVolumeSlider.value.ToString("0.00");
        });

        _mouseSensitivitySlider.onValueChanged.AddListener(_ =>
        {
            if (_mouseSensitivityValue != null)
                _mouseSensitivityValue.text = _mouseSensitivitySlider.value.ToString("0.00");
        });
    }

    void ShowSettingsPanel()
    {
        RefreshSettingsUi();
        if (_mainPanel != null)
            _mainPanel.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
    }

    void ShowMainPanel()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
        if (_mainPanel != null)
            _mainPanel.SetActive(true);
    }

    void ApplySettings()
    {
        PlayerSettings settings = ResolvePlayerSettings();
        if (settings == null)
            return;

        if (_vSyncToggle != null)
            settings.EnableVSync = _vSyncToggle.isOn;
        if (_motionBlurToggle != null)
            settings.EnableMotionBlur = _motionBlurToggle.isOn;
        if (_fpsSlider != null)
            settings.TargetFrameRate = Mathf.RoundToInt(_fpsSlider.value);
        if (_masterVolumeSlider != null)
            settings.MasterVolume = _masterVolumeSlider.value;
        if (_menuMusicVolumeSlider != null)
            settings.MenuMusicVolume = _menuMusicVolumeSlider.value;
        if (_mouseSensitivitySlider != null)
            settings.MouseSensitivity = _mouseSensitivitySlider.value;

        settings.ApplyAndSave();
        RefreshSettingsUi();
    }

    void RefreshSettingsUi()
    {
        PlayerSettings settings = ResolvePlayerSettings();
        if (settings == null)
            return;

        if (_vSyncToggle != null)
            _vSyncToggle.isOn = settings.EnableVSync;

        if (_motionBlurToggle != null)
            _motionBlurToggle.isOn = settings.EnableMotionBlur;

        if (_fpsSlider != null)
            _fpsSlider.value = settings.TargetFrameRate;

        if (_masterVolumeSlider != null)
            _masterVolumeSlider.value = settings.MasterVolume;

        if (_menuMusicVolumeSlider != null)
            _menuMusicVolumeSlider.value = settings.MenuMusicVolume;

        if (_mouseSensitivitySlider != null)
            _mouseSensitivitySlider.value = settings.MouseSensitivity;

        if (_fpsValue != null)
            _fpsValue.text = $"{Mathf.RoundToInt(_fpsSlider != null ? _fpsSlider.value : settings.TargetFrameRate)} FPS";
        if (_masterVolumeValue != null)
            _masterVolumeValue.text = (_masterVolumeSlider != null ? _masterVolumeSlider.value : settings.MasterVolume).ToString("0.00");
        if (_menuMusicVolumeValue != null)
            _menuMusicVolumeValue.text = (_menuMusicVolumeSlider != null ? _menuMusicVolumeSlider.value : settings.MenuMusicVolume).ToString("0.00");
        if (_mouseSensitivityValue != null)
            _mouseSensitivityValue.text = (_mouseSensitivitySlider != null ? _mouseSensitivitySlider.value : settings.MouseSensitivity).ToString("0.00");
    }

    void QuitToMainMenu()
    {
        if (_isOpen)
            ClosePauseMenu();

        TemporaryMainMenu menu = FindFirstObjectByType<TemporaryMainMenu>();
        if (menu != null)
        {
            menu.ReturnToMenuFreshBoot();
            return;
        }

        if (loadMainMenuSceneOnQuit && !string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    void SetGameplayInputEnabled(bool enabled)
    {
        if (_controller == null)
            _controller = FindFirstObjectByType<FirstPersonController>();
        if (_interaction == null)
            _interaction = FindFirstObjectByType<PlayerInteraction>();
        if (_flashlight == null)
            _flashlight = FindFirstObjectByType<PlayerFlashlight>();
        if (_sleepSystem == null)
            _sleepSystem = FindFirstObjectByType<PlayerSleepSystem>();

        bool shouldEnableController = enabled && !(_sleepSystem != null && _sleepSystem.IsInBag);

        if (_controller != null)
            _controller.SetControlsEnabled(shouldEnableController);

        if (_interaction != null)
            _interaction.enabled = shouldEnableController;

        if (_flashlight != null)
            _flashlight.SetToggleInputEnabled(enabled);
    }

    PlayerSettings ResolvePlayerSettings()
    {
        PlayerSettings settings = PlayerSettings.Instance != null ? PlayerSettings.Instance : FindFirstObjectByType<PlayerSettings>();
        if (settings != null)
            return settings;

        SystemsRootBootstrap systemsRoot = FindFirstObjectByType<SystemsRootBootstrap>();
        if (systemsRoot != null)
            settings = systemsRoot.GetComponent<PlayerSettings>();

        if (settings == null)
            settings = gameObject.AddComponent<PlayerSettings>();

        settings.ReloadFromDiskAndApply();
        return settings;
    }

    GameObject CreatePanel(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go;
    }

    Text CreateLabel(string name, Transform parent, string text, int fontSize, Color color, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<Text>();
        label.font = GetFont();
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;

        return label;
    }

    Button CreateButton(string name, Transform parent, string text, Sprite sprite, Color tint, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var label = textGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 24;
        label.color = bodyTextColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;

        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    Toggle CreateToggleRow(Transform parent, string label, Vector2 anchor)
    {
        var row = CreatePanel($"{label}Row", parent, fieldBackgroundSprite, new Color(0.1f, 0.1f, 0.12f, 0.85f));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = anchor;
        rowRect.anchorMax = anchor;
        rowRect.sizeDelta = new Vector2(640f, 58f);

        CreateLabel("Label", row.transform, label, 22, bodyTextColor, new Vector2(0.25f, 0.5f), new Vector2(280f, 48f));

        var toggleGo = new GameObject("Toggle");
        toggleGo.transform.SetParent(row.transform, false);
        var bg = toggleGo.AddComponent<Image>();
        bg.sprite = fieldBackgroundSprite;
        bg.type = fieldBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0.1f, 0.2f, 0.16f, 0.95f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = bg;

        RectTransform rect = bg.rectTransform;
        rect.anchorMin = new Vector2(0.82f, 0.5f);
        rect.anchorMax = new Vector2(0.82f, 0.5f);
        rect.sizeDelta = new Vector2(96f, 40f);

        var checkGo = new GameObject("Checkmark");
        checkGo.transform.SetParent(toggleGo.transform, false);
        var check = checkGo.AddComponent<Image>();
        check.color = new Color(0.72f, 0.95f, 0.82f, 0.95f);
        check.rectTransform.anchorMin = new Vector2(0.12f, 0.15f);
        check.rectTransform.anchorMax = new Vector2(0.88f, 0.85f);
        check.rectTransform.offsetMin = Vector2.zero;
        check.rectTransform.offsetMax = Vector2.zero;

        toggle.graphic = check;
        return toggle;
    }

    Slider CreateSliderRow(Transform parent, string label, Vector2 anchor, out Text valueText, float min, float max, bool whole)
    {
        var row = CreatePanel($"{label}Row", parent, fieldBackgroundSprite, new Color(0.1f, 0.1f, 0.12f, 0.85f));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = anchor;
        rowRect.anchorMax = anchor;
        rowRect.sizeDelta = new Vector2(640f, 58f);

        CreateLabel("Label", row.transform, label, 22, bodyTextColor, new Vector2(0.2f, 0.5f), new Vector2(240f, 48f));

        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(row.transform, false);
        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;

        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.52f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.82f, 0.5f);
        sliderRect.sizeDelta = new Vector2(220f, 20f);

        var bg = new GameObject("Background").AddComponent<Image>();
        bg.transform.SetParent(sliderGo.transform, false);
        bg.sprite = fieldBackgroundSprite;
        bg.type = fieldBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        bg.rectTransform.anchorMin = new Vector2(0f, 0.25f);
        bg.rectTransform.anchorMax = new Vector2(1f, 0.75f);
        bg.rectTransform.offsetMin = Vector2.zero;
        bg.rectTransform.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(6f, 0f);
        fillAreaRect.offsetMax = new Vector2(-6f, 0f);

        var fill = new GameObject("Fill").AddComponent<Image>();
        fill.transform.SetParent(fillArea.transform, false);
        fill.sprite = sliderFillSprite;
        fill.type = sliderFillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        fill.color = new Color(0.72f, 0.9f, 1f, 0.95f);
        fill.rectTransform.anchorMin = new Vector2(0f, 0.2f);
        fill.rectTransform.anchorMax = new Vector2(1f, 0.8f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle").AddComponent<Image>();
        handle.transform.SetParent(sliderGo.transform, false);
        handle.sprite = secondaryButtonSprite;
        handle.type = secondaryButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        handle.color = new Color(0.93f, 0.97f, 1f, 0.95f);
        handle.rectTransform.sizeDelta = new Vector2(16f, 30f);

        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;

        valueText = CreateLabel("Value", row.transform, string.Empty, 18, bodyTextColor, new Vector2(0.92f, 0.5f), new Vector2(90f, 40f));
        return slider;
    }

    Font GetFont()
    {
        if (uiFontOverride != null)
            return uiFontOverride;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static void EnsureEventSystem()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystem = eventSystemGo.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        var standalone = eventSystem.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            Object.Destroy(standalone);
    }
}
