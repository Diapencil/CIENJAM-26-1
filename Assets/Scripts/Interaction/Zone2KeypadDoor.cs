// Zone2KeypadDoor.cs
// Feature: Opens a zone door only after the keypad has been obtained.
// Usage: Attach to the door interaction object, assign Door, then wire TryOpen() to Interactable.onInteract.
//        Use disableOnOpen/enableOnOpen/navMeshBlockers to expand accessible NavMesh routes when the door opens.

using UnityEngine;
using UnityEngine.AI;

public class Zone2KeypadDoor : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Door door;
    [SerializeField] private bool unlockGate1OnOpen = true;

    [Header("Access Expansion")]
    [SerializeField] private GameObject[] disableOnOpen;
    [SerializeField] private GameObject[] enableOnOpen;
    [SerializeField] private NavMeshObstacle[] navMeshBlockers;

    [Header("Feedback")]
    [SerializeField] private string lockedMessage = "A keypad is required.";
    [SerializeField] private string openedMessage = "Door unlocked.";

    private bool _opened;

    private void Awake()
    {
        if (door == null)
            door = GetComponentInChildren<Door>();
    }

    public void TryOpen()
    {
        if (_opened)
        {
            Debug.Log("[Zone2KeypadDoor] TryOpen ignored because the door is already opened.", this);
            return;
        }

        if (GameManager.Current == null)
        {
            Debug.LogWarning("[Zone2KeypadDoor] GameManager is missing. Door cannot check keypad state.", this);
            return;
        }

        if (!GameManager.Current.KeypadObtained)
        {
            Debug.Log("[Zone2KeypadDoor] Door locked. Keypad has not been obtained.", this);
            ShowMessage(lockedMessage);
            return;
        }

        _opened = true;

        if (door != null)
            door.Open();
        else
            Debug.LogWarning("[Zone2KeypadDoor] Door reference is missing. Progress will unlock, but no door animation will play.", this);

        ExpandAccess();

        if (unlockGate1OnOpen)
            GameManager.Current.UnlockGate1();

        ShowMessage(openedMessage);
        Debug.Log("[Zone2KeypadDoor] Door opened with keypad. Zone access expanded.", this);
    }

    private void ExpandAccess()
    {
        SetActive(disableOnOpen, false);
        SetActive(enableOnOpen, true);

        if (navMeshBlockers == null) return;

        foreach (NavMeshObstacle blocker in navMeshBlockers)
        {
            if (blocker == null) continue;
            blocker.carving = false;
            blocker.enabled = false;
        }
    }

    private static void SetActive(GameObject[] targets, bool value)
    {
        if (targets == null) return;

        foreach (GameObject target in targets)
        {
            if (target != null)
                target.SetActive(value);
        }
    }

    private static void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        MonologueManager.GetOrCreate().Show(message, queue: false);
    }
}
