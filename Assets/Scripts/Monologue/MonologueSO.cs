// MonologueSO.cs
// Feature: Stores one reusable monologue line for bottom-screen narration text.
// Usage: Create from Assets > Create > Monologue > Monologue, then pass it to MonologueManager.Current.Show(asset).

using UnityEngine;

[CreateAssetMenu(
    fileName = "MonologueSO",
    menuName = "Monologue/Monologue")]
public class MonologueSO : ScriptableObject
{
    [Header("Content")]
    [TextArea(2, 6)]
    public string text;

    [Header("Timing")]
    [Tooltip("Seconds to keep the text visible after typing finishes. Use a negative value to let the manager choose.")]
    public float holdSeconds = -1f;
}
