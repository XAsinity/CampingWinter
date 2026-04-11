using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TemporaryMainMenu : MonoBehaviour
{
    [Header("Startup Gating")]
    [SerializeField] bool pauseTimeWhileMenuOpen = true;
    [SerializeField] bool lockCursorOnPlay = true;
    [SerializeField, Min(0f)] float postLoadBufferSeconds = 3f;

    [Header("Important Systems (Enable First)")]
    [SerializeField] GameObject[] importantGameplayObjectsToEnableFirst;
    [SerializeField] Behaviour[] importantGameplayBehavioursToEnableFirst;

    [Header("Marked Systems")]
    [SerializeField] GameObject[] gameplayObjectsToEnableOnPlay;
    [SerializeField] Behaviour[] gameplayBehavioursToEnableOnPlay;

    [Header("UI")]
    [SerializeField] string gameTitle = "Camping Winter";
    [SerializeField] string playButtonText = "Play";

    [Header("Events")]
    [SerializeField] UnityEvent onPlayPressed;

    GameObject _menuCanvasObject;
    GameObject _menuPanelObject;
    GameObject _loadingPanelObject;
    Image _loadingProgressFill;
    Text _loadingStatusText;
    Camera _menuCamera;
    Coroutine _startGameRoutine;
    bool _hasStartedGame;
    bool _isStartingGame;

    void Awake()
    {
        ReturnToMenu();
    }

    void OnDestroy()
    {
        if (_hasStartedGame)
            return;

        if (pauseTimeWhileMenuOpen)
            Time.timeScale = 1f;
    }

    public void StartGame()
    {
        if (_hasStartedGame || _isStartingGame)
            return;

        _isStartingGame = true;
        Time.timeScale = 0f;

        SetMenuVisualState(showMenu: false, showLoading: true);
        if (_startGameRoutine != null)
            StopCoroutine(_startGameRoutine);

        _startGameRoutine = StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        int totalSteps = GetEnableStepCount();
        int completedSteps = 0;

        SetLoadingProgress(0f, "Initializing important systems...");
        EnableGroup(importantGameplayObjectsToEnableFirst, importantGameplayBehavioursToEnableFirst, ref completedSteps, totalSteps);

        yield return null;

        SetLoadingProgress(GetStepProgress(completedSteps, totalSteps), "Initializing marked systems...");
        EnableGroup(gameplayObjectsToEnableOnPlay, gameplayBehavioursToEnableOnPlay, ref completedSteps, totalSteps);

        yield return null;

        float loadedProgress = GetStepProgress(completedSteps, totalSteps);
        float elapsed = 0f;
        while (elapsed < postLoadBufferSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = postLoadBufferSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / postLoadBufferSeconds);
            SetLoadingProgress(Mathf.Lerp(loadedProgress, 1f, t), "Finalizing...");
            yield return null;
        }

        SetLoadingProgress(1f, "Starting...");

        Time.timeScale = 1f;

        _hasStartedGame = true;
        _isStartingGame = false;
        _startGameRoutine = null;

        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        DestroyMenuUi();

        onPlayPressed?.Invoke();
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

        SetGameplayEnabled(false);

        if (_menuCanvasObject == null)
            BuildTemporaryMenuUi();

        SetMenuVisualState(showMenu: true, showLoading: false);

        EnsureMenuCameraIfNeeded();

        if (pauseTimeWhileMenuOpen)
            Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ReturnToMenuFreshBoot()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else
            SceneManager.LoadScene(scene.name);
    }

    void SetGameplayEnabled(bool enabled)
    {
        SetGroupEnabled(importantGameplayObjectsToEnableFirst, importantGameplayBehavioursToEnableFirst, enabled);
        SetGroupEnabled(gameplayObjectsToEnableOnPlay, gameplayBehavioursToEnableOnPlay, enabled);
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

        var canvas = _menuCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        _menuCanvasObject.AddComponent<CanvasScaler>();
        _menuCanvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(_menuCanvasObject.transform, false);
        _menuPanelObject = panelGo;
        var panel = panelGo.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0.7f);

        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 44;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = gameTitle;

        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.62f);
        titleRect.anchorMax = new Vector2(0.5f, 0.62f);
        titleRect.sizeDelta = new Vector2(700f, 70f);
        titleRect.anchoredPosition = Vector2.zero;

        var buttonGo = new GameObject("PlayButton");
        buttonGo.transform.SetParent(panelGo.transform, false);

        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.45f, 0.2f, 0.95f);

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(StartGame);

        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.48f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.48f);
        buttonRect.sizeDelta = new Vector2(220f, 60f);
        buttonRect.anchoredPosition = Vector2.zero;

        var buttonTextGo = new GameObject("Text");
        buttonTextGo.transform.SetParent(buttonGo.transform, false);
        var buttonText = buttonTextGo.AddComponent<Text>();
        buttonText.font = font;
        buttonText.fontSize = 28;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.text = playButtonText;

        RectTransform buttonTextRect = buttonText.rectTransform;
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        BuildLoadingUi();
        SetMenuVisualState(showMenu: true, showLoading: false);
    }

    void BuildLoadingUi()
    {
        _loadingPanelObject = new GameObject("LoadingPanel");
        _loadingPanelObject.transform.SetParent(_menuCanvasObject.transform, false);

        var loadingBg = _loadingPanelObject.AddComponent<Image>();
        loadingBg.color = new Color(0f, 0f, 0f, 0.78f);

        RectTransform loadingPanelRect = loadingBg.rectTransform;
        loadingPanelRect.anchorMin = Vector2.zero;
        loadingPanelRect.anchorMax = Vector2.one;
        loadingPanelRect.offsetMin = Vector2.zero;
        loadingPanelRect.offsetMax = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var loadingTitleGo = new GameObject("LoadingTitle");
        loadingTitleGo.transform.SetParent(_loadingPanelObject.transform, false);
        var loadingTitle = loadingTitleGo.AddComponent<Text>();
        loadingTitle.font = font;
        loadingTitle.fontSize = 36;
        loadingTitle.alignment = TextAnchor.MiddleCenter;
        loadingTitle.color = Color.white;
        loadingTitle.text = "Loading...";

        RectTransform loadingTitleRect = loadingTitle.rectTransform;
        loadingTitleRect.anchorMin = new Vector2(0.5f, 0.6f);
        loadingTitleRect.anchorMax = new Vector2(0.5f, 0.6f);
        loadingTitleRect.sizeDelta = new Vector2(700f, 60f);
        loadingTitleRect.anchoredPosition = Vector2.zero;

        var loadingStatusGo = new GameObject("LoadingStatus");
        loadingStatusGo.transform.SetParent(_loadingPanelObject.transform, false);
        _loadingStatusText = loadingStatusGo.AddComponent<Text>();
        _loadingStatusText.font = font;
        _loadingStatusText.fontSize = 22;
        _loadingStatusText.alignment = TextAnchor.MiddleCenter;
        _loadingStatusText.color = new Color(1f, 1f, 1f, 0.9f);
        _loadingStatusText.text = "Preparing...";

        RectTransform statusRect = _loadingStatusText.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 0.53f);
        statusRect.anchorMax = new Vector2(0.5f, 0.53f);
        statusRect.sizeDelta = new Vector2(700f, 40f);
        statusRect.anchoredPosition = Vector2.zero;

        var barBgGo = new GameObject("LoadingProgressBG");
        barBgGo.transform.SetParent(_loadingPanelObject.transform, false);
        var barBg = barBgGo.AddComponent<Image>();
        barBg.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform barBgRect = barBg.rectTransform;
        barBgRect.anchorMin = new Vector2(0.35f, 0.45f);
        barBgRect.anchorMax = new Vector2(0.65f, 0.49f);
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;

        var barFillGo = new GameObject("LoadingProgressFill");
        barFillGo.transform.SetParent(barBgGo.transform, false);
        _loadingProgressFill = barFillGo.AddComponent<Image>();
        _loadingProgressFill.color = new Color(0.73f, 0.9f, 1f, 0.95f);
        _loadingProgressFill.type = Image.Type.Filled;
        _loadingProgressFill.fillMethod = Image.FillMethod.Horizontal;
        _loadingProgressFill.fillOrigin = 0;
        _loadingProgressFill.fillAmount = 0f;

        RectTransform barFillRect = _loadingProgressFill.rectTransform;
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(1f, 1f);
        barFillRect.offsetMin = new Vector2(3f, 3f);
        barFillRect.offsetMax = new Vector2(-3f, -3f);
    }

    void SetMenuVisualState(bool showMenu, bool showLoading)
    {
        if (_menuPanelObject != null)
            _menuPanelObject.SetActive(showMenu);

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

    void SetLoadingProgress(float value01, string status)
    {
        if (_loadingProgressFill != null)
            _loadingProgressFill.fillAmount = Mathf.Clamp01(value01);

        if (_loadingStatusText != null)
            _loadingStatusText.text = status;
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
        _menuPanelObject = null;
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
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<InputSystemUIInputModule>();
    }
}
