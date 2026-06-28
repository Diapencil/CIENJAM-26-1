// Zone3KeyDoor.cs
// Feature: Opens a zone door only after the key has been obtained.
// Usage: Attach to the door interaction object, assign Door, then wire TryOpen() to Interactable.onInteract.
//        Use disableOnOpen/enableOnOpen/navMeshBlockers to expand accessible NavMesh routes when the door opens.

using UnityEngine;
using UnityEngine.AI;

public class Zone3KeyDoor : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Door door;
    [SerializeField] private bool unlockGate2OnOpen = true;

    [Header("Access Expansion")]
    [SerializeField] private GameObject[] disableOnOpen;
    [SerializeField] private GameObject[] enableOnOpen;
    [SerializeField] private NavMeshObstacle[] navMeshBlockers;

    [Header("Feedback")]
    [SerializeField] private string lockedMessage = "A key is required.";
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
            Debug.Log("[Zone3KeyDoor] TryOpen ignored because the door is already opened.", this);
            return;
        }

        if (GameManager.Current == null)
        {
            Debug.LogWarning("[Zone3KeyDoor] GameManager is missing. Door cannot check key state.", this);
            return;
        }

        if (!GameManager.Current.KeyObtained)
        {
            Debug.Log("[Zone3KeyDoor] Door locked. Key has not been obtained.", this);
            ShowMessage(lockedMessage);
            return;
        }

        _opened = true;
        AudioManager.Instance.PlaySound("거대한 게이트 열림", transform, 1, 1);

        ExpandAccess();

        // NavMesh 리베이크는 문 트윈이 완전히 끝난 뒤 실행해야 문 위치에서 메쉬가 끊기지 않는다.
        if (door != null)
            door.Open(OnDoorOpened);
        else
        {
            Debug.LogWarning("[Zone3KeyDoor] Door reference is missing. Progress will unlock, but no door animation will play.", this);
            OnDoorOpened();
        }

        ShowMessage(openedMessage);
        Debug.Log("[Zone3KeyDoor] Door opened with key. Zone access expanded.", this);
    }

    private void OnDoorOpened()
    {
        if (unlockGate2OnOpen && GameManager.Current != null)
            GameManager.Current.UnlockGate2();
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
