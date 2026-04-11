using UnityEngine;
using UnityEngine.Events;

public class NightEvent : MonoBehaviour
{
    [Header("Event Lifecycle")]
    [SerializeField] UnityEvent onTriggered;
    [SerializeField] UnityEvent onReset;

    bool _hasTriggered;

    public bool HasTriggered => _hasTriggered;

    public void TriggerEvent()
    {
        _hasTriggered = true;
        onTriggered?.Invoke();
        OnTriggeredByNightSystem();
    }

    public void ResetEventState()
    {
        _hasTriggered = false;
        onReset?.Invoke();
        OnResetByNightSystem();
    }

    protected virtual void OnTriggeredByNightSystem()
    {
    }

    protected virtual void OnResetByNightSystem()
    {
    }
}
