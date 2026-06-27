// PuangDeathReporter.cs
// Feature: Converts PuangAI catch events into the central GameManager death flow.
// Usage: Attach to the same GameObject as PuangAI, or assign a PuangAI reference.

using UnityEngine;

public class PuangDeathReporter : MonoBehaviour
{
    [SerializeField] private PuangAI puangAI;
    [SerializeField] private string deathReason = "Caught by Puang.";

    private void Awake()
    {
        if (puangAI == null)
            puangAI = GetComponent<PuangAI>();
    }

    private void OnEnable()
    {
        if (puangAI != null)
        {
            puangAI.OnCaughtPlayer += ReportDeath;
            Debug.Log("[PuangDeathReporter] Subscribed to PuangAI.OnCaughtPlayer.", this);
        }
        else
        {
            Debug.LogWarning("[PuangDeathReporter] PuangAI reference is missing. Death on catch will not be reported.", this);
        }
    }

    private void OnDisable()
    {
        if (puangAI != null)
            puangAI.OnCaughtPlayer -= ReportDeath;
    }

    private void ReportDeath()
    {
        Debug.Log($"[PuangDeathReporter] Puang caught player. Reporting death. reason='{deathReason}'", this);
        GameManager.Current?.KillPlayer(deathReason, this);
    }
}
