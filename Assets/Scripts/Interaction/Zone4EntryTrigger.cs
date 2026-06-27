// Zone4EntryTrigger.cs
// Feature: Detects the player entering zone 4. On first entry it closes the entry door
//          (no way back), teleports Puang into ambush, and starts the final escape phase.
// Usage: Put a trigger Collider (isTrigger) on the zone-4 entrance. Attach this component,
//        assign entryDoor (closed behind the player), puang, and teleportPoint.
//        The player object must be tagged "Player".

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Zone4EntryTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("진입 후 닫혀서 되돌아갈 수 없게 만들 문")]
    [SerializeField] private Door entryDoor;
    [Tooltip("최종 추적을 맡을 푸앙이")]
    [SerializeField] private PuangAI puang;
    [Tooltip("푸앙이가 텔레포트해 잠복할 지점")]
    [SerializeField] private Transform teleportPoint;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private bool _triggered;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(playerTag)) return;

        _triggered = true;

        if (entryDoor != null)
            entryDoor.Close();
        else
            Debug.LogWarning("[Zone4EntryTrigger] entryDoor is missing. Player can still walk back.", this);

        if (puang != null && teleportPoint != null)
            puang.BeginFinalAmbush(teleportPoint.position);
        else
            Debug.LogWarning("[Zone4EntryTrigger] puang or teleportPoint is missing. Final ambush not started.", this);

        if (GameManager.Current != null)
            GameManager.Current.TriggerFinalEscape();
        else
            Debug.LogWarning("[Zone4EntryTrigger] GameManager is missing. Final escape phase not started.", this);

        Debug.Log("[Zone4EntryTrigger] Zone 4 entered. Entry door closed, Puang ambush started, final escape begun.", this);
    }
}
