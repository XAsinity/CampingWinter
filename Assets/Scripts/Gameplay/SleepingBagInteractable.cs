using UnityEngine;

public class SleepingBagInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] string interactionPrompt = "Sleep";

    [Header("Anchors")]
    [SerializeField] Transform sitAnchor;
    [SerializeField] Transform layAnchor;

    public string InteractionPrompt => interactionPrompt;
    public Transform SitAnchor => sitAnchor;
    public Transform LayAnchor => layAnchor;

    public void Interact(GameObject interactor)
    {
        if (interactor == null) return;

        var sleepSystem = interactor.GetComponent<PlayerSleepSystem>();
        if (sleepSystem != null)
            sleepSystem.EnterBag(this);
    }
}
