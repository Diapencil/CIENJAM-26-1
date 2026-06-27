// MonologueView.cs
// Feature: UI Toolkit view for bottom-screen monologue text. It owns presentation and typewriter display only.
// Usage: Add this to the same GameObject as a UIDocument using Monologue.uxml. MonologueManager calls Show/Hide.

using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MonologueView : MonoBehaviour
{
    [Header("Typewriter")]
    [SerializeField, Min(0)] long charIntervalMs = 24;

    [SerializeField] UIDocument uiDocument;

    VisualElement root;
    VisualElement panel;
    Label body;
    string fullText = "";
    int typedCount;
    IVisualElementScheduledItem typeTask;

    public bool IsTyping { get; private set; }

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (root == null)
        {
            Debug.LogError("[MonologueView] rootVisualElement is missing. Check UIDocument Source Asset.", this);
            return;
        }

        panel = root.Q<VisualElement>("monologue-root");
        body = root.Q<Label>("monologue-body");

        Hide();
    }

    public void Show(string text)
    {
        if (panel == null || body == null) return;

        panel.style.display = DisplayStyle.Flex;
        StartTyping(text);
    }

    public void Hide()
    {
        StopTyping();
        IsTyping = false;

        if (body != null)
            body.text = "";

        if (panel != null)
            panel.style.display = DisplayStyle.None;
    }

    public void SkipTyping()
    {
        if (!IsTyping) return;

        if (body != null)
            body.text = fullText;

        StopTyping();
        IsTyping = false;
    }

    public float GetTypingSeconds(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        return text.Length * (charIntervalMs / 1000f);
    }

    void StartTyping(string text)
    {
        fullText = text ?? "";
        typedCount = 0;

        StopTyping();

        if (body != null)
            body.text = "";

        if (fullText.Length == 0)
        {
            IsTyping = false;
            return;
        }

        IsTyping = true;
        typeTask = root.schedule.Execute(TypeNextCharacter).Every(charIntervalMs);
    }

    void TypeNextCharacter()
    {
        typedCount++;

        if (body != null)
            body.text = fullText.Substring(0, typedCount);

        if (typedCount < fullText.Length) return;

        StopTyping();
        IsTyping = false;
    }

    void StopTyping()
    {
        typeTask?.Pause();
        typeTask = null;
    }
}
