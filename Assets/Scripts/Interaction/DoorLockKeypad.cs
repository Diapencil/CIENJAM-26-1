// DoorLockKeypad.cs
// Feature: Final escape door lock. Opens the keypad UI on interaction; on the correct
//          4-digit password it opens the door, freezes Puang, and triggers the ending.
// Usage: Attach to the door-lock world object and wire OpenKeypad() to Interactable.onInteract.
//        Assign correctPassword (4 digits), the DoorLockPanelController, the Door to open,
//        and the PuangAI to freeze. Puang keeps chasing while the keypad is open, so being
//        caught during input is an immediate death (no cancel).

using UnityEngine;

public class DoorLockKeypad : MonoBehaviour
{
    [Header("Password")]
    [Tooltip("정답 비밀번호(4자리). 기존까지 촬영한 사진들에서 유추 가능한 값")]
    [SerializeField] private string correctPassword = "0000";

    [Header("References")]
    [SerializeField] private DoorLockPanelController keypadPanel;
    [Tooltip("해제 성공 시 열릴 탈출문")]
    [SerializeField] private Door exitDoor;
    [Tooltip("해제 성공 시 정지시킬 푸앙이")]
    [SerializeField] private PuangAI puang;

    [Header("Input Lock")]
    [Tooltip("키패드를 여는 동안 비활성화할 동작(플레이어 이동/시점/플래시 등). 입력 확정이라 재활성화하지 않음")]
    [SerializeField] private Behaviour[] disableWhileOpen;

    private bool _solved;

    private void Awake()
    {
        if (keypadPanel == null)
            keypadPanel = FindAnyObjectByType<DoorLockPanelController>();
    }

    /// <summary>Interactable.onInteract 에 연결. 키패드 UI 를 연다.</summary>
    public void OpenKeypad()
    {
        if (_solved) return;

        if (keypadPanel == null)
        {
            Debug.LogWarning("[DoorLockKeypad] keypadPanel is missing. Cannot open keypad.", this);
            return;
        }

        DisableInputBehaviours();
        keypadPanel.Open(correctPassword, OnUnlocked);
        Debug.Log("[DoorLockKeypad] Keypad opened.", this);
    }

    private void DisableInputBehaviours()
    {
        if (disableWhileOpen == null) return;
        foreach (Behaviour target in disableWhileOpen)
        {
            if (target == null) continue;
            target.enabled = false;
        }
    }

    private void OnUnlocked()
    {
        if (_solved) return;
        _solved = true;

        if (exitDoor != null)
            exitDoor.Open();
        else
            Debug.LogWarning("[DoorLockKeypad] exitDoor is missing. No door open motion will play.", this);

        if (puang != null)
            puang.FreezeFinal();
        else
            Debug.LogWarning("[DoorLockKeypad] puang is missing. Puang will not be frozen.", this);

        if (GameManager.Current != null)
            GameManager.Current.TriggerEnding();
        else
            Debug.LogWarning("[DoorLockKeypad] GameManager is missing. Ending not triggered.", this);

        Debug.Log("[DoorLockKeypad] Door unlocked. Door opened, Puang frozen, ending triggered.", this);
    }
}
