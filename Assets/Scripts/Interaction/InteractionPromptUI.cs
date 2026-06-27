// InteractionPromptUI.cs
// Feature: Runtime key prompt that follows the active interactable's screen position.
// Usage: InteractionManager calls Show/Hide automatically; no prefab setup is required.

using UnityEngine;
using UnityEngine.UI;

public class InteractionPromptUI : MonoBehaviour
{
    static InteractionPromptUI instance;

    Canvas canvas;
    RectTransform promptRect;
    Text keyText;

    public static InteractionPromptUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InteractionPromptUI>();
                if (instance == null)
                {
                    var go = new GameObject(nameof(InteractionPromptUI));
                    instance = go.AddComponent<InteractionPromptUI>();
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
        BuildIfNeeded();
        Hide();
    }

    public void Show(string keyLabel, Vector3 screenPosition)
    {
        BuildIfNeeded();

        keyText.text = string.IsNullOrWhiteSpace(keyLabel) ? "F" : keyLabel;
        promptRect.position = screenPosition;

        if (!promptRect.gameObject.activeSelf)
            promptRect.gameObject.SetActive(true);
    }

    public void Hide()
    {
        BuildIfNeeded();
        promptRect.gameObject.SetActive(false);
    }

    void BuildIfNeeded()
    {
        if (promptRect != null && keyText != null) return;

        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("InteractionPromptCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        var promptGo = new GameObject("KeyPrompt");
        promptGo.transform.SetParent(canvas.transform, false);
        promptRect = promptGo.AddComponent<RectTransform>();
        promptRect.sizeDelta = new Vector2(42f, 42f);

        var image = promptGo.AddComponent<Image>();
        image.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

        var textGo = new GameObject("KeyText");
        textGo.transform.SetParent(promptGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        keyText = textGo.AddComponent<Text>();
        keyText.text = "F";
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = Color.white;
        keyText.fontSize = 24;
        keyText.fontStyle = FontStyle.Bold;
        keyText.raycastTarget = false;
        keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
