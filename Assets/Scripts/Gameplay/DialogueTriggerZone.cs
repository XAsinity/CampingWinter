using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DialogueTriggerZone : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] string dialogueKey = "MAP_BORDER_WARNING";
    [SerializeField] string dialogueText = "I think i should go back before i get lost.";
    [SerializeField] float subtitleDuration = 3.5f;

    [Header("Trigger Rules")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool triggerOnce = false;
    [SerializeField] float cooldownSeconds = 3f;
    [SerializeField] bool allowStayReminder = false;
    [SerializeField] float stayReminderInterval = 5f;

    bool _hasTriggered;
    float _nextAllowedTime;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        TryTrigger(other.gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        if (!allowStayReminder)
            return;

        TryTrigger(other.gameObject, useStayInterval: true);
    }

    void TryTrigger(GameObject other, bool useStayInterval = false)
    {
        if (other == null)
            return;

        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return;

        if (triggerOnce && _hasTriggered)
            return;

        float requiredDelay = useStayInterval ? stayReminderInterval : cooldownSeconds;
        if (Time.time < _nextAllowedTime)
            return;

        var interaction = other.GetComponentInParent<PlayerInteraction>();
        if (interaction == null)
            return;

        interaction.ShowSubtitle(dialogueText, subtitleDuration);

        _hasTriggered = true;
        _nextAllowedTime = Time.time + Mathf.Max(0f, requiredDelay);
    }
}
