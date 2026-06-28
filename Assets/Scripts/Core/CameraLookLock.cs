// CameraLookLock.cs
// Feature: Global gate that suppresses first-person camera look (mouse rotation).
//          While any owner holds a lock, Player_Ctrl.Rotate() early-returns so the mouse
//          can't move the view. Used by UIs that need the cursor free (door lock keypad, photo album).
// Usage: UI 표시 시 CameraLookLock.RequestLocked(this), 닫을 때 CameraLookLock.Release(this).
//        시점 회전 측은 if (CameraLookLock.IsLocked) return; 로 확인한다.

using System.Collections.Generic;

public static class CameraLookLock
{
    private static readonly HashSet<object> LockOwners = new();

    /// <summary>잠금 보유자가 하나라도 있으면 시점 회전을 막는다.</summary>
    public static bool IsLocked => LockOwners.Count > 0;

    public static void RequestLocked(object owner)
    {
        if (owner == null) return;
        LockOwners.Add(owner);
    }

    public static void Release(object owner)
    {
        if (owner == null) return;
        LockOwners.Remove(owner);
    }
}
