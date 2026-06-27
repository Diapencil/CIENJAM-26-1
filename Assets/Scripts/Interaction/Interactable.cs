// Interactable.cs
// Feature: Attach this to any world object that should react to the interaction key (e.g. opening doors, picking up batteries).
// Usage: Set the interaction key, range, optional prompt anchor, and wire the behaviour through the onInteract UnityEvent.

using UnityEngine;
using UnityEngine.Events;

[System.Flags]
public enum InteractionType
{
    None   = 0,
    Custom = 1 << 0, // 1
}

public class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] InteractionType interactionType = InteractionType.Custom;
    [SerializeField] KeyCode interactionKey = KeyCode.F;
    [SerializeField, Min(0.1f)] float interactionRange = 2.5f;
    [SerializeField] bool interactOnce;
    [SerializeField] bool hidePromptAfterInteractOnce = true;

    [Header("Prompt")]
    [SerializeField] Transform promptAnchor;
    [SerializeField] Vector3 promptWorldOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] string promptText = "F";

    [Header("Outline")]
    [SerializeField] bool disableHighlightOnInteract;

    [Header("Events")]
    [SerializeField] UnityEvent onInteract;

    bool hasInteracted;

    public InteractionType InteractionType => interactionType;
    public KeyCode InteractionKey => interactionKey;
    public float InteractionRange => interactionRange;
    public string PromptText => string.IsNullOrWhiteSpace(promptText) ? interactionKey.ToString() : promptText;
    public bool CanShowPrompt => isActiveAndEnabled
                                 && (!hasInteracted || !hidePromptAfterInteractOnce);
    public bool CanInteract => isActiveAndEnabled
                               && (!hasInteracted || !interactOnce);

    void OnEnable()
    {
        InteractionManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (InteractionManager.HasInstance)
            InteractionManager.Instance.Unregister(this);
    }

    public Vector3 GetPromptWorldPosition()
    {
        Transform anchor = promptAnchor != null ? promptAnchor : transform;
        return anchor.position + promptWorldOffset;
    }

    public Vector3 GetRangeCenter()
    {
        Transform anchor = promptAnchor != null ? promptAnchor : transform;
        return anchor.position;
    }

    public void Interact()
    {
        if (!CanInteract) return;

        onInteract?.Invoke();

        if (disableHighlightOnInteract)
        {
            var outline = GetComponent<RuntimeOutline>();
            if (outline != null)
                outline.SetHighlighted(false);
        }

        if (interactOnce)
            hasInteracted = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.35f);
        Gizmos.DrawWireSphere(GetRangeCenter(), interactionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetPromptWorldPosition(), 0.08f);
    }
}
