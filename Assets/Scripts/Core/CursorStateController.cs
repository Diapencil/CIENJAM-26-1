using System.Collections.Generic;
using UnityEngine;

public static class CursorStateController
{
    private static readonly HashSet<object> LockRequests = new();
    private static readonly HashSet<object> UnlockRequests = new();

    public static void RequestLocked(object owner)
    {
        if (owner == null) return;
        UnlockRequests.Remove(owner);
        LockRequests.Add(owner);
        Apply();
    }

    public static void RequestUnlocked(object owner)
    {
        if (owner == null) return;
        LockRequests.Remove(owner);
        UnlockRequests.Add(owner);
        Apply();
    }

    public static void Release(object owner)
    {
        if (owner == null) return;
        LockRequests.Remove(owner);
        UnlockRequests.Remove(owner);
        Apply();
    }

    private static void Apply()
    {
        if (UnlockRequests.Count > 0)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            return;
        }

        if (LockRequests.Count > 0)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            return;
        }

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }
}
