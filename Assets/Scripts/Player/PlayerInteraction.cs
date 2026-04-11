using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public interface IInteractable
{
    string InteractionPrompt { get; }
    void Interact(GameObject interactor);
}

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] float interactRange = 3.5f;
    [SerializeField, Min(0f)] float interactRadius = 0.2f;
    [SerializeField] LayerMask interactMask = ~0;

    [Header("Interaction UI")]
    [SerializeField] bool showReticle = true;
    [SerializeField, Range(0f, 1f)] float idleReticleAlpha = 0.2f;
    [SerializeField, Range(0f, 1f)] float hoverReticleAlpha = 0.45f;
    [SerializeField] bool showPromptText = true;
    [SerializeField] string promptPrefix = "[E] ";

    [Header("Subtitle Dialogue UI")]
    [SerializeField] bool showSubtitleText = true;
    [SerializeField] float defaultSubtitleDuration = 2.5f;

    Camera _cam;
    IInteractable _currentTarget;
    Text _reticleText;
    Text _promptText;
    Text _subtitleText;
    float _subtitleHideTime;

    public IInteractable CurrentTarget => _currentTarget;

    void Start()
    {
        _cam = GetComponentInChildren<Camera>();
        EnsureInteractionUi();
    }

    void OnDisable()
    {
        _currentTarget = null;
        UpdateInteractionUi();
        HideSubtitle();
    }

    void Update()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        RaycastHit hit;
        bool hasHit = interactRadius > 0f
            ? Physics.SphereCast(ray, interactRadius, out hit, interactRange, interactMask, QueryTriggerInteraction.Collide)
            : Physics.Raycast(ray, out hit, interactRange, interactMask, QueryTriggerInteraction.Collide);

        if (hasHit)
        {
            _currentTarget = hit.collider.GetComponentInParent<IInteractable>();
        }
        else
        {
            _currentTarget = null;
        }

        UpdateInteractionUi();
        UpdateSubtitleUi();

        if (_currentTarget != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            _currentTarget.Interact(gameObject);
        }
    }

    void EnsureInteractionUi()
    {
        if (!showReticle && !showPromptText)
            return;

        var canvasGo = new GameObject("InteractionUI");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (showReticle)
        {
            var reticleGo = new GameObject("Reticle");
            reticleGo.transform.SetParent(canvasGo.transform, false);
            _reticleText = reticleGo.AddComponent<Text>();
            _reticleText.font = font;
            _reticleText.fontSize = 20;
            _reticleText.alignment = TextAnchor.MiddleCenter;
            _reticleText.text = "●";
            _reticleText.color = new Color(1f, 1f, 1f, idleReticleAlpha);

            RectTransform rect = _reticleText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(20f, 20f);
            rect.anchoredPosition = Vector2.zero;
        }

        if (showPromptText)
        {
            var promptGo = new GameObject("InteractPrompt");
            promptGo.transform.SetParent(canvasGo.transform, false);
            _promptText = promptGo.AddComponent<Text>();
            _promptText.font = font;
            _promptText.fontSize = 16;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = new Color(1f, 1f, 1f, 0.85f);
            _promptText.text = string.Empty;
            _promptText.enabled = false;

            RectTransform rect = _promptText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(450f, 30f);
            rect.anchoredPosition = new Vector2(0f, -28f);
        }

        if (showSubtitleText)
        {
            var subtitleGo = new GameObject("SubtitlePrompt");
            subtitleGo.transform.SetParent(canvasGo.transform, false);
            _subtitleText = subtitleGo.AddComponent<Text>();
            _subtitleText.font = font;
            _subtitleText.fontSize = 24;
            _subtitleText.alignment = TextAnchor.MiddleCenter;
            _subtitleText.color = new Color(1f, 1f, 1f, 0.9f);
            _subtitleText.text = string.Empty;
            _subtitleText.enabled = false;

            RectTransform rect = _subtitleText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(900f, 80f);
            rect.anchoredPosition = new Vector2(0f, 70f);
        }
    }

    void UpdateInteractionUi()
    {
        bool hovering = _currentTarget != null;

        if (_reticleText != null)
        {
            Color c = _reticleText.color;
            c.a = hovering ? hoverReticleAlpha : idleReticleAlpha;
            _reticleText.color = c;
        }

        if (_promptText != null)
        {
            string prompt = hovering ? _currentTarget.InteractionPrompt : null;
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                _promptText.text = promptPrefix + prompt;
                _promptText.enabled = true;
            }
            else
            {
                _promptText.text = string.Empty;
                _promptText.enabled = false;
            }
        }
    }

    void UpdateSubtitleUi()
    {
        if (_subtitleText == null || !_subtitleText.enabled)
            return;

        if (Time.time >= _subtitleHideTime)
            HideSubtitle();
    }

    void HideSubtitle()
    {
        if (_subtitleText == null)
            return;

        _subtitleText.text = string.Empty;
        _subtitleText.enabled = false;
    }

    public void ShowSubtitle(string message, float duration)
    {
        if (_subtitleText == null || string.IsNullOrWhiteSpace(message))
            return;

        _subtitleText.text = message;
        _subtitleText.enabled = true;

        float d = duration > 0f ? duration : defaultSubtitleDuration;
        _subtitleHideTime = Time.time + d;
    }
}
