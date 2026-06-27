// InteractionManager.cs
// Feature: Chooses the active interactable in range and handles the interaction key.
// Usage: Created automatically by Interactable. Place Player in the scene or assign a Player tag for range checks.

using System.Collections.Generic;
using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    static InteractionManager instance;

    readonly List<Interactable> interactables = new();
    Interactable active;
    Transform player;
    Camera mainCamera;

    public static bool HasInstance => instance != null;

    public static InteractionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InteractionManager>();
                if (instance == null)
                {
                    var go = new GameObject(nameof(InteractionManager));
                    instance = go.AddComponent<InteractionManager>();
                }
            }

            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Update()
    {
        RefreshReferences();
        active = FindBestInteractable();

        if (active == null)
        {
            InteractionPromptUI.Instance.Hide();
            return;
        }

        InteractionPromptUI.Instance.Show(active.PromptText, GetScreenPosition(active));

        if (UserInput.Instance.GetKeyDown(active.InteractionKey))
            active.Interact();
    }

    public void Register(Interactable interactable)
    {
        if (interactable == null || interactables.Contains(interactable)) return;
        interactables.Add(interactable);
    }

    public void Unregister(Interactable interactable)
    {
        if (interactable == null) return;
        interactables.Remove(interactable);
        if (active == interactable)
            active = null;
    }

    void RefreshReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (player != null) return;

        var playerComponent = FindObjectOfType<Player_Ctrl>();
        if (playerComponent != null)
        {
            player = playerComponent.transform;
            return;
        }

        var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
            player = taggedPlayer.transform;
    }

    Interactable FindBestInteractable()
    {
        if (player == null || mainCamera == null) return null;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Interactable best = null;
        float bestScore = float.MaxValue;

        for (int i = interactables.Count - 1; i >= 0; i--)
        {
            var interactable = interactables[i];
            if (interactable == null)
            {
                interactables.RemoveAt(i);
                continue;
            }

            if (!interactable.CanShowPrompt || !interactable.CanInteract)
                continue;

            float distance = Vector3.Distance(player.position, interactable.GetRangeCenter());
            if (distance > interactable.InteractionRange)
                continue;

            Vector3 screenPosition = GetScreenPosition(interactable);
            if (screenPosition.z <= 0f)
                continue;

            float score = ((Vector2)screenPosition - screenCenter).sqrMagnitude;
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = interactable;
        }

        return best;
    }

    Vector3 GetScreenPosition(Interactable interactable)
    {
        return mainCamera.WorldToScreenPoint(interactable.GetPromptWorldPosition());
    }
}
