// Zone2KeypadPickup.cs
// Feature: Reports zone-2 keypad pickup to GameManager.
// Usage: Attach to the keypad object and wire PickUp() to Interactable.onInteract.

using UnityEngine;

public class Zone2KeypadPickup : MonoBehaviour
{
    [SerializeField] private bool deactivateAfterPickup = true;
    [SerializeField] private string pickupMessage = "Keypad obtained.";

    public void PickUp()
    {
        if (GameManager.Current == null)
        {
            Debug.LogWarning("[Zone2KeypadPickup] GameManager is missing. Keypad pickup ignored.", this);
            return;
        }

        bool alreadyObtained = GameManager.Current.KeypadObtained;
        GameManager.Current.ObtainKeypad();

        if (!alreadyObtained && !string.IsNullOrWhiteSpace(pickupMessage))
            MonologueManager.GetOrCreate().Show(pickupMessage, queue: false);

        Debug.Log($"[Zone2KeypadPickup] PickUp called. alreadyObtained={alreadyObtained}", this);

        if (deactivateAfterPickup)
            gameObject.SetActive(false);
    }
}
