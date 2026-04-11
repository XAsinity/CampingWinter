using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerSleepSystem : MonoBehaviour
{
    [Header("Sleep Controls")]
    [SerializeField] Key sleepHoldKey = Key.Space;
    [SerializeField] Key exitBedKey = Key.Q;
    [SerializeField] float transitionSpeed = 8f;
    [SerializeField] float sleepYawOffsetDegrees = -90f;

    [Header("Fallback Bag Anchors")]
    [SerializeField] Vector3 fallbackSitWorldOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField] Vector3 fallbackLayWorldOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] float fallbackBagYawOffsetDegrees = 0f;

    [Header("Sleep Progress")]
    [SerializeField, Range(0f, 1f)] float sleepProgressGainPerSecond = 0.12f;
    [SerializeField, Range(0f, 1f)] float sleepProgressDecayPerSecond = 0f;

    [Header("Sitting Camera Look Limits")]
    [SerializeField] float sitLookSensitivity = 0.12f;
    [SerializeField, Range(0f, 120f)] float sitYawLimit = 65f;
    [SerializeField, Range(0f, 80f)] float sitLookUpLimit = 30f;
    [SerializeField, Range(0f, 80f)] float sitLookDownLimit = 35f;

    FirstPersonController _controller;
    PlayerInteraction _interaction;
    Camera _playerCamera;
    CharacterController _characterController;

    SleepingBagInteractable _activeBag;
    Vector3 _defaultCamLocalPos;
    Quaternion _defaultCamLocalRot;
    Image _sleepOverlay;
    CanvasGroup _sleepProgressCanvasGroup;
    Image _sleepProgressFill;
    Sprite _uiSprite;

    bool _isInBag;
    bool _isHoldingSleep;
    bool _wasCrouchingBeforeBag;
    float _sitYawOffset;
    float _sitPitchOffset;
    float _sleepProgress;

    public float SleepProgress01 => _sleepProgress;
    public bool IsHoldingSleep => _isHoldingSleep;
    public bool IsInBag => _isInBag;

    void Start()
    {
        _controller = GetComponent<FirstPersonController>();
        _interaction = GetComponent<PlayerInteraction>();
        _characterController = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>();

        if (_playerCamera != null)
        {
            _defaultCamLocalPos = _playerCamera.transform.localPosition;
            _defaultCamLocalRot = _playerCamera.transform.localRotation;
        }

        CreateSleepOverlay();
    }

    void Update()
    {
        if (!_isInBag || _activeBag == null || _playerCamera == null)
        {
            _isHoldingSleep = false;
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[exitBedKey].wasPressedThisFrame)
        {
            ExitBag();
            return;
        }

        bool holdingSleep = keyboard[sleepHoldKey].isPressed;
        _isHoldingSleep = holdingSleep;
        Vector3 targetPosition;
        Quaternion targetRotation;

        if (holdingSleep)
        {
            // Sleeping = no camera movement.
            targetPosition = GetLayTargetPosition(_activeBag);
            float layYaw = GetLayYaw(_activeBag);
            targetRotation = Quaternion.Euler(0f, layYaw + sleepYawOffsetDegrees, 0f);
            _sitYawOffset = 0f;
            _sitPitchOffset = 0f;

            _sleepProgress += sleepProgressGainPerSecond * Time.deltaTime;
        }
        else
        {
            // Sitting = limited look range.
            Transform sit = _activeBag.SitAnchor;
            targetPosition = GetSitTargetPosition(_activeBag);

            Quaternion sitBaseRotation = GetSittingBaseRotation(sit, _activeBag.transform, fallbackBagYawOffsetDegrees);
            ApplySittingLookOffsets();

            targetRotation = sitBaseRotation * Quaternion.Euler(_sitPitchOffset, _sitYawOffset, 0f);

            _sleepProgress -= sleepProgressDecayPerSecond * Time.deltaTime;
        }

        _sleepProgress = Mathf.Clamp01(_sleepProgress);

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * transitionSpeed);
        _playerCamera.transform.rotation = Quaternion.Slerp(_playerCamera.transform.rotation, targetRotation, Time.deltaTime * transitionSpeed);

        if (_sleepOverlay != null)
        {
            Color c = _sleepOverlay.color;
            c.a = Mathf.Lerp(c.a, holdingSleep ? 1f : 0f, Time.deltaTime * transitionSpeed);
            _sleepOverlay.color = c;
        }

        if (_sleepProgressCanvasGroup != null)
        {
            float target = holdingSleep ? 1f : 0f;
            _sleepProgressCanvasGroup.alpha = Mathf.Lerp(_sleepProgressCanvasGroup.alpha, target, Time.deltaTime * transitionSpeed);
        }

        if (_sleepProgressFill != null)
            _sleepProgressFill.fillAmount = _sleepProgress;
    }

    public void EnterBag(SleepingBagInteractable bag)
    {
        if (bag == null || _isInBag) return;

        _activeBag = bag;
        _isInBag = true;

        if (_controller != null)
        {
            _wasCrouchingBeforeBag = _controller.IsCrouching;
            _controller.ForceCrouch(true);
        }

        if (_controller != null)
            _controller.SetControlsEnabled(false);

        if (_interaction != null)
            _interaction.enabled = false;

        if (_characterController != null)
            _characterController.enabled = false;

        transform.position = GetSitTargetPosition(_activeBag);
        _sitYawOffset = 0f;
        _sitPitchOffset = 0f;
    }

    public void ExitBag()
    {
        _isInBag = false;

        if (_controller != null)
            _controller.ForceCrouch(_wasCrouchingBeforeBag);

        if (_controller != null)
            _controller.SetControlsEnabled(true);

        if (_interaction != null)
            _interaction.enabled = true;

        if (_characterController != null)
            _characterController.enabled = true;

        if (_playerCamera != null)
        {
            _playerCamera.transform.localPosition = _defaultCamLocalPos;
            _playerCamera.transform.localRotation = _defaultCamLocalRot;
        }

        if (_sleepOverlay != null)
        {
            Color c = _sleepOverlay.color;
            c.a = 0f;
            _sleepOverlay.color = c;
        }

        if (_sleepProgressCanvasGroup != null)
            _sleepProgressCanvasGroup.alpha = 0f;

        _activeBag = null;
        _isHoldingSleep = false;
    }

    public void ConfigureSleepProgressRates(float gainPerSecond, float decayPerSecond)
    {
        sleepProgressGainPerSecond = Mathf.Max(0f, gainPerSecond);
        sleepProgressDecayPerSecond = Mathf.Max(0f, decayPerSecond);
    }

    public void ResetSleepProgress()
    {
        _sleepProgress = 0f;
        if (_sleepProgressFill != null)
            _sleepProgressFill.fillAmount = 0f;
    }

    void CreateSleepOverlay()
    {
        var canvasGo = new GameObject("SleepOverlayCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("SleepOverlay");
        imageGo.transform.SetParent(canvasGo.transform, false);

        _sleepOverlay = imageGo.AddComponent<Image>();
        _sleepOverlay.sprite = GetUiSprite();
        _sleepOverlay.type = Image.Type.Sliced;
        _sleepOverlay.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rect = _sleepOverlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Temporary progress UI for sleeping state.
        var progressCanvasGo = new GameObject("SleepProgressCanvas");
        progressCanvasGo.transform.SetParent(transform, false);
        var progressCanvas = progressCanvasGo.AddComponent<Canvas>();
        progressCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        progressCanvas.sortingOrder = 1001;
        progressCanvasGo.AddComponent<CanvasScaler>();
        progressCanvasGo.AddComponent<GraphicRaycaster>();

        var progressRoot = new GameObject("SleepProgressRoot", typeof(RectTransform));
        progressRoot.transform.SetParent(progressCanvasGo.transform, false);
        _sleepProgressCanvasGroup = progressRoot.AddComponent<CanvasGroup>();
        _sleepProgressCanvasGroup.alpha = 0f;

        RectTransform progressRootRect = progressRoot.GetComponent<RectTransform>();
        progressRootRect.anchorMin = Vector2.zero;
        progressRootRect.anchorMax = Vector2.one;
        progressRootRect.offsetMin = Vector2.zero;
        progressRootRect.offsetMax = Vector2.zero;

        var progressBg = new GameObject("SleepProgressBG");
        progressBg.transform.SetParent(progressRoot.transform, false);
        var bgImage = progressBg.AddComponent<Image>();
        bgImage.sprite = GetUiSprite();
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform bgRect = bgImage.rectTransform;
        bgRect.anchorMin = new Vector2(0.35f, 0.07f);
        bgRect.anchorMax = new Vector2(0.65f, 0.11f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var progressFill = new GameObject("SleepProgressFill");
        progressFill.transform.SetParent(progressBg.transform, false);
        _sleepProgressFill = progressFill.AddComponent<Image>();
        _sleepProgressFill.sprite = GetUiSprite();
        _sleepProgressFill.color = new Color(0.72f, 0.86f, 1f, 0.95f);
        _sleepProgressFill.type = Image.Type.Filled;
        _sleepProgressFill.fillMethod = Image.FillMethod.Horizontal;
        _sleepProgressFill.fillOrigin = 0;
        _sleepProgressFill.fillAmount = _sleepProgress;

        RectTransform fillRect = _sleepProgressFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
    }

    void ApplySittingLookOffsets()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        _sitYawOffset += delta.x * sitLookSensitivity;
        _sitPitchOffset -= delta.y * sitLookSensitivity;

        _sitYawOffset = Mathf.Clamp(_sitYawOffset, -sitYawLimit, sitYawLimit);
        _sitPitchOffset = Mathf.Clamp(_sitPitchOffset, -sitLookUpLimit, sitLookDownLimit);
    }

    Vector3 GetSitTargetPosition(SleepingBagInteractable bag)
    {
        if (bag == null)
            return transform.position;

        Transform sit = bag.SitAnchor;
        if (sit == null || sit == bag.transform)
            return bag.transform.position + fallbackSitWorldOffset;

        return sit.position;
    }

    Vector3 GetLayTargetPosition(SleepingBagInteractable bag)
    {
        if (bag == null)
            return transform.position;

        Transform lay = bag.LayAnchor;
        if (lay == null || lay == bag.transform)
            return bag.transform.position + fallbackLayWorldOffset;

        return lay.position;
    }

    float GetLayYaw(SleepingBagInteractable bag)
    {
        if (bag == null)
            return transform.rotation.eulerAngles.y;

        Transform lay = bag.LayAnchor;
        if (lay == null || lay == bag.transform)
            return bag.transform.eulerAngles.y + fallbackBagYawOffsetDegrees;

        return lay.rotation.eulerAngles.y;
    }

    static Quaternion GetSittingBaseRotation(Transform sitAnchor, Transform bagTransform, float fallbackBagYawOffset)
    {
        if (sitAnchor == null || sitAnchor == bagTransform)
            return Quaternion.Euler(0f, bagTransform.eulerAngles.y + fallbackBagYawOffset, 0f);

        Transform source = sitAnchor;
        // Keep sitting orientation upright and only inherit yaw.
        Vector3 euler = source.rotation.eulerAngles;
        return Quaternion.Euler(0f, euler.y, 0f);
    }

    Sprite GetUiSprite()
    {
        if (_uiSprite != null)
            return _uiSprite;

        _uiSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f));

        return _uiSprite;
    }
}
