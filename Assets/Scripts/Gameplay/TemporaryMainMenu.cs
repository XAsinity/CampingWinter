using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TemporaryMainMenu : MonoBehaviour
{
    static TemporaryMainMenu _instance;

    [Serializable]
    class SaveSlotData
    {
        public bool hasData;
        public int currentNight = 1;
        public int deathCount;
        public string createdAtUtc;
        public string lastSavedAtUtc;
    }

    [Serializable]
    class SaveDataRoot
    {
        public int activeSlotIndex;
        public SaveSlotData[] slots = new SaveSlotData[3]
        {
            new SaveSlotData(),
            new SaveSlotData(),
            new SaveSlotData()
        };
    }

    enum MenuScreen
    {
        Main,
        Settings,
        SaveSelect
    }

    [Header("Startup Gating")]
    [SerializeField] bool pauseTimeWhileMenuOpen = true;
    [SerializeField] bool lockCursorOnPlay = true;
    [SerializeField, Min(0f)] float postLoadBufferSeconds = 3f;
    [SerializeField] bool enableDebugMenuVisibilityToggle = true;
    [SerializeField] Key debugMenuVisibilityToggleKey = Key.F10;

    [Header("Scene Transition")]
    [SerializeField] bool loadGameplaySceneOnPlay = true;
    [SerializeField] string gameplaySceneName = "CampingWinter";
    [SerializeField] bool startNightAfterSceneLoad = true;
    [SerializeField] bool loadMainMenuSceneOnReturn = true;
    [SerializeField] string mainMenuSceneName = "MainMenu";

    [Header("Important Systems (Enable First)")]
    [SerializeField] GameObject[] importantGameplayObjectsToEnableFirst;
    [SerializeField] Behaviour[] importantGameplayBehavioursToEnableFirst;

    [Header("Marked Systems")]
    [SerializeField] GameObject[] gameplayObjectsToEnableOnPlay;
    [SerializeField] Behaviour[] gameplayBehavioursToEnableOnPlay;

    [Header("UI Text")]
    [SerializeField] string gameTitle = "Camping Winter";
    [SerializeField] string playButtonText = "Play";
    [SerializeField] string settingsButtonText = "Settings";
    [SerializeField] string quitButtonText = "Quit";
    [SerializeField] string backButtonText = "Back";
    [SerializeField] string saveSelectTitle = "Select Save";

    [Header("UI Style")]
    [SerializeField] Font uiFontOverride;
    [SerializeField, Min(10)] int titleFontSize = 44;
    [SerializeField, Min(10)] int bodyFontSize = 22;
    [SerializeField, Min(10)] int smallFontSize = 18;
    [SerializeField] Color titleTextColor = new Color(0.82f, 0.92f, 1f, 1f);
    [SerializeField] Color bodyTextColor = new Color(0.9f, 0.95f, 1f, 0.95f);
    [SerializeField] Color panelTint = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] Color primaryButtonTint = new Color(0.2f, 0.4f, 0.26f, 0.95f);
    [SerializeField] Color secondaryButtonTint = new Color(0.16f, 0.23f, 0.35f, 0.95f);
    [SerializeField] Color dangerButtonTint = new Color(0.45f, 0.17f, 0.17f, 0.95f);
    [SerializeField] Color fieldTint = new Color(0.1f, 0.1f, 0.12f, 0.85f);

    [Header("UI Image Overrides")]
    [SerializeField] Sprite mainPanelSprite;
    [SerializeField] Sprite settingsPanelSprite;
    [SerializeField] Sprite saveSelectPanelSprite;
    [SerializeField] Sprite loadingPanelSprite;
    [SerializeField] Sprite primaryButtonSprite;
    [SerializeField] Sprite secondaryButtonSprite;
    [SerializeField] Sprite dangerButtonSprite;
    [SerializeField] Sprite fieldBackgroundSprite;
    [SerializeField] Sprite slotRowSprite;
    [SerializeField] Sprite loadingProgressBackgroundSprite;
    [SerializeField] Sprite loadingProgressFillSprite;

    [Header("Events")]
    [SerializeField] UnityEvent onPlayPressed;

    GameObject _menuCanvasObject;
    GameObject _mainPanelObject;
    GameObject _settingsPanelObject;
    GameObject _saveSelectPanelObject;
    GameObject _loadingPanelObject;

    Image _loadingProgressFill;
    Text _loadingStatusText;

    Toggle _settingsVSyncToggle;
    Toggle _settingsMotionBlurToggle;
    Slider _settingsFpsSlider;
    Text _settingsFpsValueText;
    Slider _settingsMasterVolumeSlider;
    Text _settingsMasterValueText;
    Slider _settingsMenuMusicVolumeSlider;
    Text _settingsMenuMusicValueText;
    Slider _settingsMouseSensitivitySlider;
    Text _settingsMouseSensitivityValueText;

    Text[] _saveSlotInfoTexts;
    Button[] _saveSlotLoadButtons;
    Button[] _saveSlotDeleteButtons;

    SaveDataRoot _menuSaveData;
    string _menuSaveFilePath;

    Camera _menuCamera;
    Coroutine _startGameRoutine;
    bool _hasStartedGame;
    bool _isStartingGame;
    MenuScreen _currentScreen;

    FirstPersonController _runtimeController;
    PlayerInteraction _runtimeInteraction;
    PlayerFlashlight _runtimeFlashlight;
    bool _menuHiddenByDebug;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        _menuSaveFilePath = System.IO.Path.Combine(Application.persistentDataPath, "save-slots.json");
        LoadMenuSaveData();
        ShowMenuState();
    }

    void Update()
    {
        if (!enableDebugMenuVisibilityToggle)
            return;

        if (_hasStartedGame || _menuCanvasObject == null)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!keyboard[debugMenuVisibilityToggleKey].wasPressedThisFrame)
            return;

        SetMenuUiHiddenByDebug(!_menuHiddenByDebug);
    }

    public bool IsMenuUiHiddenByDebug => _menuHiddenByDebug;

    public void SetMenuUiHiddenByDebug(bool hidden)
    {
        _menuHiddenByDebug = hidden;

        if (_menuCanvasObject != null)
            _menuCanvasObject.SetActive(!_menuHiddenByDebug);

        Cursor.visible = !_menuHiddenByDebug;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnDestroy()
    {
        FlushSettingsIfOpen();

        if (_hasStartedGame)
            return;

        if (pauseTimeWhileMenuOpen)
            Time.timeScale = 1f;
    }

    void FlushSettingsIfOpen()
    {
        if (_currentScreen == MenuScreen.Settings)
            ApplySettingsFromUi();
    }

    public void StartGame()
    {
        if (_isStartingGame)
            return;

        ShowSaveSelectScreen();
    }

    void StartGameFromSelectedSave()
    {
        if (_hasStartedGame || _isStartingGame)
            return;

        _isStartingGame = true;
        Time.timeScale = 0f;

        SetMenuVisualState(showMain: false, showSettings: false, showSaveSelect: false, showLoading: true);
        if (_startGameRoutine != null)
            StopCoroutine(_startGameRoutine);

        SetRuntimeGameplayActive(false);
        _startGameRoutine = StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        int totalSteps = GetEnableStepCount();
        int completedSteps = 0;

        SetLoadingProgress(0f, "Initializing important systems...");
        EnableGroup(importantGameplayObjectsToEnableFirst, importantGameplayBehavioursToEnableFirst, ref completedSteps, totalSteps);
        SetRuntimeGameplayActive(false);

        yield return null;

        SetLoadingProgress(GetStepProgress(completedSteps, totalSteps), "Initializing marked systems...");
        EnableGroup(gameplayObjectsToEnableOnPlay, gameplayBehavioursToEnableOnPlay, ref completedSteps, totalSteps);
        SetRuntimeGameplayActive(false);

        yield return null;

        float loadedProgress = GetStepProgress(completedSteps, totalSteps);
        bool shouldSwitchScene = ShouldSwitchToGameplayScene();

        if (shouldSwitchScene)
        {
            loadedProgress = Mathf.Max(loadedProgress, 0.15f);
            yield return LoadGameplaySceneAsync(loadedProgress);
            loadedProgress = 0.9f;
            SetRuntimeGameplayActive(false);
        }

        float elapsed = 0f;
        while (elapsed < postLoadBufferSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = postLoadBufferSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / postLoadBufferSeconds);
            SetLoadingProgress(Mathf.Lerp(loadedProgress, 1f, t), "Finalizing...");
            SetRuntimeGameplayActive(false);
            yield return null;
        }

        SetLoadingProgress(1f, "Starting...");

        _hasStartedGame = true;
        _isStartingGame = false;
        _startGameRoutine = null;

        if (startNightAfterSceneLoad && shouldSwitchScene && NightSystem.Instance != null && !NightSystem.Instance.IsNightRunning)
            NightSystem.Instance.StartNight(resetTime: true);

        SetRuntimeGameplayActive(true);
        Time.timeScale = 1f;

        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        DestroyMenuUi();
        onPlayPressed?.Invoke();
    }

    bool ShouldSwitchToGameplayScene()
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
            return false;

        if (loadGameplaySceneOnPlay)
            return true;

        Scene activeScene = SceneManager.GetActiveScene();
        return !string.Equals(activeScene.name, gameplaySceneName, StringComparison.Ordinal);
    }

    IEnumerator LoadGameplaySceneAsync(float startProgress)
    {
        SetLoadingProgress(startProgress, "Loading gameplay scene...");

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
        if (loadOperation == null)
            yield break;

        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            float t = Mathf.Clamp01(loadOperation.progress / 0.9f);
            SetLoadingProgress(Mathf.Lerp(startProgress, 0.9f, t), "Loading gameplay scene...");
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
            yield return null;
    }

    public void ReturnToMenu()
    {
        if (_startGameRoutine != null)
        {
            StopCoroutine(_startGameRoutine);
            _startGameRoutine = null;
        }

        _hasStartedGame = false;
        _isStartingGame = false;

        if (ShouldSwitchToMainMenuScene())
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            return;
        }

        ShowMenuState();
    }

    public void ReturnToMenuFreshBoot()
    {
        if (_startGameRoutine != null)
        {
            StopCoroutine(_startGameRoutine);
            _startGameRoutine = null;
        }

        _hasStartedGame = false;
        _isStartingGame = false;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (ShouldSwitchToMainMenuScene())
        {
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else
            SceneManager.LoadScene(scene.name);
    }

    bool ShouldSwitchToMainMenuScene()
    {
        if (!loadMainMenuSceneOnReturn)
            return false;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            return false;

        Scene activeScene = SceneManager.GetActiveScene();
        return !string.Equals(activeScene.name, mainMenuSceneName, StringComparison.Ordinal);
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName)
            && string.Equals(scene.name, mainMenuSceneName, StringComparison.Ordinal))
        {
            ShowMenuState();
        }
    }

    void ShowMenuState()
    {
        EnsureEventSystem();
        SetGameplayEnabled(false);

        if (_menuCanvasObject == null)
            BuildTemporaryMenuUi();

        if (_menuCanvasObject != null)
            _menuCanvasObject.SetActive(true);

        SetMenuUiHiddenByDebug(false);

        CleanupDuplicateMenuCanvases();

        ShowMainScreen();
        EnsureMenuCameraIfNeeded();

        if (pauseTimeWhileMenuOpen)
            Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ShowMainScreen()
    {
        if (_currentScreen == MenuScreen.Settings)
            ApplySettingsFromUi();

        _currentScreen = MenuScreen.Main;
        SetMenuVisualState(showMain: true, showSettings: false, showSaveSelect: false, showLoading: false);
    }

    void ShowSettingsScreen()
    {
        _currentScreen = MenuScreen.Settings;
        RefreshSettingsUi();
        SetMenuVisualState(showMain: false, showSettings: true, showSaveSelect: false, showLoading: false);
    }

    void ShowSaveSelectScreen()
    {
        _currentScreen = MenuScreen.SaveSelect;
        RefreshSaveSlotUi();
        SetMenuVisualState(showMain: false, showSettings: false, showSaveSelect: true, showLoading: false);
    }

    void SetGameplayEnabled(bool enabled)
    {
        SetGroupEnabled(importantGameplayObjectsToEnableFirst, importantGameplayBehavioursToEnableFirst, enabled);
        SetGroupEnabled(gameplayObjectsToEnableOnPlay, gameplayBehavioursToEnableOnPlay, enabled);
    }

    void SetRuntimeGameplayActive(bool active)
    {
        ResolveRuntimeGameplayComponents();

        if (_runtimeController != null)
            _runtimeController.SetControlsEnabled(active);

        if (_runtimeInteraction != null)
            _runtimeInteraction.enabled = active;

        if (_runtimeFlashlight != null)
        {
            _runtimeFlashlight.SetToggleInputEnabled(active);
            if (!active)
                _runtimeFlashlight.SetFlashlightEnabled(false);
        }
    }

    void ResolveRuntimeGameplayComponents()
    {
        if (_runtimeController == null)
            _runtimeController = FindFirstObjectByType<FirstPersonController>();

        if (_runtimeInteraction == null)
            _runtimeInteraction = FindFirstObjectByType<PlayerInteraction>();

        if (_runtimeFlashlight == null)
            _runtimeFlashlight = FindFirstObjectByType<PlayerFlashlight>();
    }

    void SetGroupEnabled(GameObject[] objects, Behaviour[] behaviours, bool enabled)
    {
        if (objects != null)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                var go = objects[i];
                if (go != null)
                    go.SetActive(enabled);
            }
        }

        if (behaviours != null)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour != null)
                    behaviour.enabled = enabled;
            }
        }
    }

    void BuildTemporaryMenuUi()
    {
        EnsureEventSystem();

        _menuCanvasObject = new GameObject("TemporaryMainMenuCanvas");
        _menuCanvasObject.transform.SetParent(transform, false);

        var canvas = _menuCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        _menuCanvasObject.AddComponent<CanvasScaler>();
        _menuCanvasObject.AddComponent<GraphicRaycaster>();

        BuildMainPanel();
        BuildSettingsPanel();
        BuildSaveSelectPanel();
        BuildLoadingUi();

        ShowMainScreen();
    }

    void BuildMainPanel()
    {
        Font font = GetUiFont();

        _mainPanelObject = CreatePanel("MainPanel", _menuCanvasObject.transform, mainPanelSprite, panelTint);

        CreateLabel("Title", _mainPanelObject.transform, gameTitle, titleFontSize, titleTextColor,
            new Vector2(0.5f, 0.68f), new Vector2(700f, 90f));

        CreateButton("PlayButton", _mainPanelObject.transform, playButtonText, primaryButtonSprite, primaryButtonTint,
            new Vector2(0.5f, 0.52f), new Vector2(260f, 64f), StartGame, font);

        CreateButton("SettingsButton", _mainPanelObject.transform, settingsButtonText, secondaryButtonSprite, secondaryButtonTint,
            new Vector2(0.5f, 0.43f), new Vector2(260f, 56f), ShowSettingsScreen, font);

        CreateButton("QuitButton", _mainPanelObject.transform, quitButtonText, dangerButtonSprite, dangerButtonTint,
            new Vector2(0.5f, 0.34f), new Vector2(260f, 56f), QuitGame, font);
    }

    void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void BuildSettingsPanel()
    {
        Font font = GetUiFont();

        _settingsPanelObject = CreatePanel("SettingsPanel", _menuCanvasObject.transform, settingsPanelSprite, panelTint);
        CreateLabel("SettingsTitle", _settingsPanelObject.transform, "Settings", titleFontSize, titleTextColor,
            new Vector2(0.5f, 0.72f), new Vector2(700f, 80f));

        _settingsVSyncToggle = CreateToggleRow(_settingsPanelObject.transform, "VSync", new Vector2(0.5f, 0.60f), font);
        _settingsMotionBlurToggle = CreateToggleRow(_settingsPanelObject.transform, "Motion Blur", new Vector2(0.5f, 0.52f), font);

        _settingsFpsSlider = CreateSliderRow(_settingsPanelObject.transform, "Frame Rate", new Vector2(0.5f, 0.44f), out _settingsFpsValueText, font, 30f, 240f, true);
        _settingsFpsSlider.onValueChanged.AddListener(_ =>
        {
            if (_settingsFpsValueText != null)
                _settingsFpsValueText.text = $"{Mathf.RoundToInt(_settingsFpsSlider.value)} FPS";
        });

        _settingsMasterVolumeSlider = CreateSliderRow(_settingsPanelObject.transform, "Master Volume", new Vector2(0.5f, 0.36f), out _settingsMasterValueText, font, 0f, 4f, false);
        _settingsMasterVolumeSlider.onValueChanged.AddListener(_ =>
        {
            if (_settingsMasterValueText != null)
                _settingsMasterValueText.text = _settingsMasterVolumeSlider.value.ToString("0.00");
        });

        _settingsMenuMusicVolumeSlider = CreateSliderRow(_settingsPanelObject.transform, "Menu Music Volume", new Vector2(0.5f, 0.28f), out _settingsMenuMusicValueText, font, 0f, 1f, false);
        _settingsMenuMusicVolumeSlider.onValueChanged.AddListener(_ =>
        {
            if (_settingsMenuMusicValueText != null)
                _settingsMenuMusicValueText.text = _settingsMenuMusicVolumeSlider.value.ToString("0.00");
        });

        _settingsMouseSensitivitySlider = CreateSliderRow(_settingsPanelObject.transform, "Mouse Sensitivity", new Vector2(0.5f, 0.20f), out _settingsMouseSensitivityValueText, font, 0.2f, 8f, false);
        _settingsMouseSensitivitySlider.onValueChanged.AddListener(_ =>
        {
            if (_settingsMouseSensitivityValueText != null)
                _settingsMouseSensitivityValueText.text = _settingsMouseSensitivitySlider.value.ToString("0.00");
        });

        CreateButton("ApplySettingsButton", _settingsPanelObject.transform, "Apply", primaryButtonSprite, primaryButtonTint,
            new Vector2(0.42f, 0.12f), new Vector2(180f, 52f), ApplySettingsFromUi, font);

        CreateButton("BackFromSettingsButton", _settingsPanelObject.transform, backButtonText, secondaryButtonSprite, secondaryButtonTint,
            new Vector2(0.58f, 0.12f), new Vector2(180f, 52f), ShowMainScreen, font);
    }

    void BuildSaveSelectPanel()
    {
        Font font = GetUiFont();

        _saveSelectPanelObject = CreatePanel("SaveSelectPanel", _menuCanvasObject.transform, saveSelectPanelSprite, panelTint);
        CreateLabel("SaveSelectTitle", _saveSelectPanelObject.transform, saveSelectTitle, titleFontSize, titleTextColor,
            new Vector2(0.5f, 0.76f), new Vector2(800f, 80f));

        _saveSlotInfoTexts = new Text[3];
        _saveSlotLoadButtons = new Button[3];
        _saveSlotDeleteButtons = new Button[3];

        float yStart = 0.62f;
        float yStep = 0.13f;

        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            float y = yStart - i * yStep;

            var row = CreatePanel($"SaveRow_{i + 1}", _saveSelectPanelObject.transform, slotRowSprite, fieldTint);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, y);
            rowRect.anchorMax = new Vector2(0.5f, y);
            rowRect.sizeDelta = new Vector2(840f, 90f);

            _saveSlotInfoTexts[i] = CreateLabel($"SaveInfo_{i + 1}", row.transform, string.Empty, smallFontSize, bodyTextColor,
                new Vector2(0.34f, 0.5f), new Vector2(520f, 70f), TextAnchor.MiddleLeft);

            _saveSlotLoadButtons[i] = CreateButton($"LoadSlot_{i + 1}", row.transform, "Load", primaryButtonSprite, primaryButtonTint,
                new Vector2(0.76f, 0.5f), new Vector2(120f, 46f), () => SelectSaveSlot(slot), font);

            _saveSlotDeleteButtons[i] = CreateButton($"DeleteSlot_{i + 1}", row.transform, "Delete", dangerButtonSprite, dangerButtonTint,
                new Vector2(0.91f, 0.5f), new Vector2(120f, 46f), () => DeleteSaveSlot(slot), font);
        }

        CreateButton("BackFromSaveSelectButton", _saveSelectPanelObject.transform, backButtonText, secondaryButtonSprite, secondaryButtonTint,
            new Vector2(0.5f, 0.18f), new Vector2(240f, 56f), ShowMainScreen, font);
    }

    void BuildLoadingUi()
    {
        Font font = GetUiFont();

        _loadingPanelObject = CreatePanel("LoadingPanel", _menuCanvasObject.transform, loadingPanelSprite, panelTint);

        CreateLabel("LoadingTitle", _loadingPanelObject.transform, "Loading...", 36, titleTextColor,
            new Vector2(0.5f, 0.62f), new Vector2(700f, 60f));

        _loadingStatusText = CreateLabel("LoadingStatus", _loadingPanelObject.transform, "Preparing...", bodyFontSize, bodyTextColor,
            new Vector2(0.5f, 0.54f), new Vector2(700f, 40f));

        var barBg = CreatePanel("LoadingProgressBG", _loadingPanelObject.transform, loadingProgressBackgroundSprite, fieldTint);
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.35f, 0.45f);
        barBgRect.anchorMax = new Vector2(0.65f, 0.49f);
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;

        var fillGo = new GameObject("LoadingProgressFill");
        fillGo.transform.SetParent(barBg.transform, false);
        _loadingProgressFill = fillGo.AddComponent<Image>();
        _loadingProgressFill.sprite = loadingProgressFillSprite;
        _loadingProgressFill.type = loadingProgressFillSprite != null ? Image.Type.Sliced : Image.Type.Filled;
        _loadingProgressFill.color = new Color(0.73f, 0.9f, 1f, 0.95f);
        _loadingProgressFill.fillMethod = Image.FillMethod.Horizontal;
        _loadingProgressFill.fillOrigin = 0;
        _loadingProgressFill.fillAmount = 0f;

        RectTransform barFillRect = _loadingProgressFill.rectTransform;
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(1f, 1f);
        barFillRect.offsetMin = new Vector2(3f, 3f);
        barFillRect.offsetMax = new Vector2(-3f, -3f);
    }

    GameObject CreatePanel(string name, Transform parent, Sprite sprite, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go;
    }

    Text CreateLabel(string name, Transform parent, string text, int fontSize, Color color, Vector2 anchorCenter, Vector2 size, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<Text>();
        label.font = GetUiFont();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchorCenter;
        rect.anchorMax = anchorCenter;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        return label;
    }

    Button CreateButton(string name, Transform parent, string text, Sprite sprite, Color tint, Vector2 anchorCenter, Vector2 size, UnityAction onClick, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorCenter;
        rect.anchorMax = anchorCenter;
        rect.sizeDelta = size;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var label = textGo.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = bodyFontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = bodyTextColor;

        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    Toggle CreateToggleRow(Transform parent, string labelText, Vector2 anchorCenter, Font font)
    {
        var row = CreatePanel($"{labelText}Row", parent, fieldBackgroundSprite, fieldTint);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = anchorCenter;
        rowRect.anchorMax = anchorCenter;
        rowRect.sizeDelta = new Vector2(640f, 58f);

        CreateLabel("Label", row.transform, labelText, bodyFontSize, bodyTextColor,
            new Vector2(0.25f, 0.5f), new Vector2(280f, 48f), TextAnchor.MiddleLeft);

        var toggleGo = new GameObject("Toggle");
        toggleGo.transform.SetParent(row.transform, false);
        var toggleBg = toggleGo.AddComponent<Image>();
        toggleBg.sprite = fieldBackgroundSprite;
        toggleBg.type = fieldBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        toggleBg.color = new Color(0.1f, 0.2f, 0.16f, 0.95f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = toggleBg;

        RectTransform toggleRect = toggleBg.rectTransform;
        toggleRect.anchorMin = new Vector2(0.82f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.82f, 0.5f);
        toggleRect.sizeDelta = new Vector2(96f, 40f);

        var checkGo = new GameObject("Checkmark");
        checkGo.transform.SetParent(toggleGo.transform, false);
        var checkImage = checkGo.AddComponent<Image>();
        checkImage.color = new Color(0.72f, 0.95f, 0.82f, 0.95f);

        RectTransform checkRect = checkImage.rectTransform;
        checkRect.anchorMin = new Vector2(0.12f, 0.15f);
        checkRect.anchorMax = new Vector2(0.88f, 0.85f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        toggle.graphic = checkImage;
        return toggle;
    }

    Slider CreateSliderRow(Transform parent, string labelText, Vector2 anchorCenter, out Text valueText, Font font, float min, float max, bool wholeNumbers)
    {
        var row = CreatePanel($"{labelText}Row", parent, fieldBackgroundSprite, fieldTint);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = anchorCenter;
        rowRect.anchorMax = anchorCenter;
        rowRect.sizeDelta = new Vector2(640f, 58f);

        CreateLabel("Label", row.transform, labelText, bodyFontSize, bodyTextColor,
            new Vector2(0.2f, 0.5f), new Vector2(240f, 48f), TextAnchor.MiddleLeft);

        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(row.transform, false);
        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;

        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.52f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.82f, 0.5f);
        sliderRect.sizeDelta = new Vector2(220f, 20f);

        var background = new GameObject("Background").AddComponent<Image>();
        background.transform.SetParent(sliderGo.transform, false);
        background.sprite = fieldBackgroundSprite;
        background.type = fieldBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = new Color(0f, 0f, 0f, 0.8f);

        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(6f, 0f);
        fillAreaRect.offsetMax = new Vector2(-6f, 0f);

        var fill = new GameObject("Fill").AddComponent<Image>();
        fill.transform.SetParent(fillArea.transform, false);
        fill.sprite = loadingProgressFillSprite;
        fill.type = loadingProgressFillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        fill.color = new Color(0.72f, 0.9f, 1f, 0.95f);

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0.2f);
        fillRect.anchorMax = new Vector2(1f, 0.8f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle").AddComponent<Image>();
        handle.transform.SetParent(sliderGo.transform, false);
        handle.sprite = secondaryButtonSprite;
        handle.type = secondaryButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        handle.color = new Color(0.93f, 0.97f, 1f, 0.95f);

        RectTransform handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(16f, 30f);

        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;

        valueText = CreateLabel("Value", row.transform, string.Empty, smallFontSize, bodyTextColor,
            new Vector2(0.92f, 0.5f), new Vector2(90f, 40f), TextAnchor.MiddleRight);

        return slider;
    }

    Font GetUiFont()
    {
        if (uiFontOverride != null)
            return uiFontOverride;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void RefreshSettingsUi()
    {
        PlayerSettings settings = PlayerSettings.Instance != null ? PlayerSettings.Instance : FindFirstObjectByType<PlayerSettings>();
        if (settings == null)
            return;

        if (_settingsVSyncToggle != null)
            _settingsVSyncToggle.isOn = settings.EnableVSync;

        if (_settingsMotionBlurToggle != null)
            _settingsMotionBlurToggle.isOn = settings.EnableMotionBlur;

        if (_settingsFpsSlider != null)
        {
            _settingsFpsSlider.value = settings.TargetFrameRate;
            if (_settingsFpsValueText != null)
                _settingsFpsValueText.text = $"{settings.TargetFrameRate} FPS";
        }

        if (_settingsMasterVolumeSlider != null)
        {
            _settingsMasterVolumeSlider.value = settings.MasterVolume;
            if (_settingsMasterValueText != null)
                _settingsMasterValueText.text = settings.MasterVolume.ToString("0.00");
        }

        if (_settingsMenuMusicVolumeSlider != null)
        {
            _settingsMenuMusicVolumeSlider.value = settings.MenuMusicVolume;
            if (_settingsMenuMusicValueText != null)
                _settingsMenuMusicValueText.text = settings.MenuMusicVolume.ToString("0.00");
        }

        if (_settingsMouseSensitivitySlider != null)
        {
            _settingsMouseSensitivitySlider.value = settings.MouseSensitivity;
            if (_settingsMouseSensitivityValueText != null)
                _settingsMouseSensitivityValueText.text = settings.MouseSensitivity.ToString("0.00");
        }
    }

    void ApplySettingsFromUi()
    {
        PlayerSettings settings = PlayerSettings.Instance != null ? PlayerSettings.Instance : FindFirstObjectByType<PlayerSettings>();
        if (settings == null)
            return;

        if (_settingsVSyncToggle != null)
            settings.EnableVSync = _settingsVSyncToggle.isOn;

        if (_settingsMotionBlurToggle != null)
            settings.EnableMotionBlur = _settingsMotionBlurToggle.isOn;

        if (_settingsFpsSlider != null)
            settings.TargetFrameRate = Mathf.RoundToInt(_settingsFpsSlider.value);

        if (_settingsMasterVolumeSlider != null)
            settings.MasterVolume = _settingsMasterVolumeSlider.value;

        if (_settingsMenuMusicVolumeSlider != null)
            settings.MenuMusicVolume = _settingsMenuMusicVolumeSlider.value;

        if (_settingsMouseSensitivitySlider != null)
            settings.MouseSensitivity = _settingsMouseSensitivitySlider.value;

        settings.ApplyAndSave();
        RefreshSettingsUi();
    }

    void SelectSaveSlot(int slotIndex)
    {
        NightSystem nightSystem = NightSystem.Instance != null ? NightSystem.Instance : FindFirstObjectByType<NightSystem>();
        if (nightSystem != null)
        {
            if (!nightSystem.HasSaveSlotData(slotIndex))
                nightSystem.CreateOrOverrideSaveSlot(slotIndex, 1);

            nightSystem.SelectSaveSlot(slotIndex, createIfMissing: true);
        }
        else
        {
            EnsureMenuSaveSlotExists(slotIndex);
            _menuSaveData.activeSlotIndex = slotIndex;
            SaveMenuSaveData();
        }

        StartGameFromSelectedSave();
    }

    void DeleteSaveSlot(int slotIndex)
    {
        NightSystem nightSystem = NightSystem.Instance != null ? NightSystem.Instance : FindFirstObjectByType<NightSystem>();
        if (nightSystem != null)
            nightSystem.DeleteSaveSlot(slotIndex);
        else
        {
            if (slotIndex < 0 || slotIndex > 2)
                return;

            _menuSaveData.slots[slotIndex] = new SaveSlotData();
            if (_menuSaveData.activeSlotIndex == slotIndex)
                _menuSaveData.activeSlotIndex = 0;

            SaveMenuSaveData();
        }

        RefreshSaveSlotUi();
    }

    void RefreshSaveSlotUi()
    {
        if (_saveSlotInfoTexts == null)
            return;

        NightSystem nightSystem = NightSystem.Instance != null ? NightSystem.Instance : FindFirstObjectByType<NightSystem>();
        LoadMenuSaveData();

        for (int i = 0; i < 3; i++)
        {
            bool hasSave = nightSystem != null ? nightSystem.HasSaveSlotData(i) : HasMenuSaveSlotData(i);

            if (!hasSave)
            {
                _saveSlotInfoTexts[i].text = $"Save {i + 1}: Empty";
                SetButtonText(_saveSlotLoadButtons[i], "New");
                _saveSlotDeleteButtons[i].interactable = false;
                continue;
            }

            int night = nightSystem != null ? nightSystem.GetSaveSlotCurrentNight(i) : Mathf.Max(1, _menuSaveData.slots[i].currentNight);
            int deaths = nightSystem != null ? nightSystem.GetSaveSlotDeathCount(i) : Mathf.Max(0, _menuSaveData.slots[i].deathCount);
            string createdRaw = nightSystem != null ? nightSystem.GetSaveSlotCreatedAtUtc(i) : (_menuSaveData.slots[i].createdAtUtc ?? string.Empty);
            string createdDisplay = NormalizeToLocalDisplayTime(createdRaw);

            _saveSlotInfoTexts[i].text = $"Save {i + 1}: Night {night}  |  Deaths {deaths}  |  Created {createdDisplay}";
            SetButtonText(_saveSlotLoadButtons[i], "Load");
            _saveSlotDeleteButtons[i].interactable = true;
        }
    }

    void LoadMenuSaveData()
    {
        if (System.IO.File.Exists(_menuSaveFilePath))
        {
            string json = System.IO.File.ReadAllText(_menuSaveFilePath);
            if (!string.IsNullOrWhiteSpace(json))
                _menuSaveData = JsonUtility.FromJson<SaveDataRoot>(json);
        }

        if (_menuSaveData == null)
            _menuSaveData = new SaveDataRoot();

        if (_menuSaveData.slots == null || _menuSaveData.slots.Length != 3)
            _menuSaveData.slots = new SaveSlotData[3] { new SaveSlotData(), new SaveSlotData(), new SaveSlotData() };

        for (int i = 0; i < _menuSaveData.slots.Length; i++)
        {
            if (_menuSaveData.slots[i] == null)
                _menuSaveData.slots[i] = new SaveSlotData();
        }

        _menuSaveData.activeSlotIndex = Mathf.Clamp(_menuSaveData.activeSlotIndex, 0, 2);
    }

    void SaveMenuSaveData()
    {
        if (_menuSaveData == null)
            _menuSaveData = new SaveDataRoot();

        _menuSaveData.activeSlotIndex = Mathf.Clamp(_menuSaveData.activeSlotIndex, 0, 2);
        string json = JsonUtility.ToJson(_menuSaveData, true);
        WriteJsonWithBackup(_menuSaveFilePath, json);
    }

    static void WriteJsonWithBackup(string path, string json)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string backupPath = path + ".bak";
        if (System.IO.File.Exists(path))
            System.IO.File.Copy(path, backupPath, true);

        System.IO.File.WriteAllText(path, json);
    }

    bool HasMenuSaveSlotData(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return false;

        return _menuSaveData != null
            && _menuSaveData.slots != null
            && _menuSaveData.slots[slotIndex] != null
            && _menuSaveData.slots[slotIndex].hasData;
    }

    void EnsureMenuSaveSlotExists(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 2)
            return;

        LoadMenuSaveData();
        if (HasMenuSaveSlotData(slotIndex))
            return;

        string now = DateTime.Now.ToString("g");
        _menuSaveData.slots[slotIndex] = new SaveSlotData
        {
            hasData = true,
            currentNight = 1,
            deathCount = 0,
            createdAtUtc = now,
            lastSavedAtUtc = now
        };

        SaveMenuSaveData();
    }

    static string NormalizeToLocalDisplayTime(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (raw.Contains("Z") || raw.Contains("+") || raw.Contains("GMT") || raw.Contains("UTC"))
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset offset))
                return offset.ToLocalTime().ToString("g");

            if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out DateTime utcDate))
                return utcDate.ToLocalTime().ToString("g");
        }

        if (DateTime.TryParse(raw, out DateTime localDate))
            return localDate.ToString("g");

        return raw;
    }

    void CleanupDuplicateMenuCanvases()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject == _menuCanvasObject)
                continue;

            if (canvas.name == "TemporaryMainMenuCanvas")
                Destroy(canvas.gameObject);
        }
    }

    void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.text = text;
    }

    void SetLoadingProgress(float value01, string status)
    {
        if (_loadingProgressFill != null)
            _loadingProgressFill.fillAmount = Mathf.Clamp01(value01);

        if (_loadingStatusText != null)
            _loadingStatusText.text = status;
    }

    void SetMenuVisualState(bool showMain, bool showSettings, bool showSaveSelect, bool showLoading)
    {
        if (_mainPanelObject != null)
            _mainPanelObject.SetActive(showMain);

        if (_settingsPanelObject != null)
            _settingsPanelObject.SetActive(showSettings);

        if (_saveSelectPanelObject != null)
            _saveSelectPanelObject.SetActive(showSaveSelect);

        if (_loadingPanelObject != null)
            _loadingPanelObject.SetActive(showLoading);
    }

    int GetEnableStepCount()
    {
        int count = 0;
        count += CountValid(importantGameplayObjectsToEnableFirst);
        count += CountValid(importantGameplayBehavioursToEnableFirst);
        count += CountValid(gameplayObjectsToEnableOnPlay);
        count += CountValid(gameplayBehavioursToEnableOnPlay);
        return Mathf.Max(1, count);
    }

    void EnableGroup(GameObject[] objects, Behaviour[] behaviours, ref int completedSteps, int totalSteps)
    {
        if (objects != null)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null)
                    continue;

                objects[i].SetActive(true);
                completedSteps++;
                SetLoadingProgress(GetStepProgress(completedSteps, totalSteps), "Initializing systems...");
            }
        }

        if (behaviours != null)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                    continue;

                behaviours[i].enabled = true;
                completedSteps++;
                SetLoadingProgress(GetStepProgress(completedSteps, totalSteps), "Initializing systems...");
            }
        }
    }

    static float GetStepProgress(int completedSteps, int totalSteps)
    {
        if (totalSteps <= 0)
            return 1f;

        return Mathf.Clamp01((float)completedSteps / totalSteps);
    }

    static int CountValid(GameObject[] values)
    {
        if (values == null)
            return 0;

        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
                count++;
        }

        return count;
    }

    static int CountValid(Behaviour[] values)
    {
        if (values == null)
            return 0;

        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
                count++;
        }

        return count;
    }

    void DestroyMenuUi()
    {
        if (_menuCanvasObject != null)
            Destroy(_menuCanvasObject);

        if (_menuCamera != null)
            Destroy(_menuCamera.gameObject);

        _menuCamera = null;
        _menuCanvasObject = null;
        _mainPanelObject = null;
        _settingsPanelObject = null;
        _saveSelectPanelObject = null;
        _loadingPanelObject = null;
        _loadingProgressFill = null;
        _loadingStatusText = null;
    }

    void EnsureMenuCameraIfNeeded()
    {
        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled)
                return;
        }

        var cameraGo = new GameObject("TemporaryMainMenuCamera");
        _menuCamera = cameraGo.AddComponent<Camera>();
        _menuCamera.clearFlags = CameraClearFlags.SolidColor;
        _menuCamera.backgroundColor = Color.black;
        _menuCamera.cullingMask = 0;
        _menuCamera.nearClipPlane = 0.01f;
        _menuCamera.farClipPlane = 10f;
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
            UnityEngine.Object.Destroy(standalone);
    }
}
