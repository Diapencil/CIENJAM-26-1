// MonologueManager.cs
// Feature: Scene-scoped manager for bottom-screen monologue text that can be requested from gameplay, cutscenes, or events.
// Usage:
//   MonologueManager.Current.Show("This place feels familiar...");
//   MonologueManager.Current.Show(monologueAsset);
// Place this component in a scene and connect a MonologueView. If view is empty, it tries to find one in the scene.

using System.Collections.Generic;
using UnityEngine;

public class MonologueManager : DomainSingleton<MonologueManager>
{
    private readonly struct Request
    {
        public readonly string Text;
        public readonly float HoldSeconds;

        public Request(string text, float holdSeconds)
        {
            Text = text;
            HoldSeconds = holdSeconds;
        }
    }

    [Header("View")]
    [SerializeField] MonologueView view;

    [Header("Timing")]
    [SerializeField, Min(0f)] float defaultHoldSeconds = 2f;

    [Tooltip("Extra hold time added per visible character when holdSeconds is negative.")]
    [SerializeField, Min(0f)] float holdSecondsPerCharacter = 0.035f;

    [Header("Behavior")]
    [Tooltip("When true, new monologues wait until the current one finishes. When false, new monologues replace current text.")]
    [SerializeField] bool queueByDefault = true;

    readonly Queue<Request> queue = new();
    Request current;
    bool isShowing;
    bool warnedMissingView;
    float hideAtTime;

    public bool IsShowing => isShowing;

    public static MonologueManager GetOrCreate()
    {
        if (Current != null)
            return Current;

        var existing = FindAnyObjectByType<MonologueManager>();
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(MonologueManager));
        return go.AddComponent<MonologueManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (view == null)
            view = FindAnyObjectByType<MonologueView>();
    }

    void Update()
    {
        if (!isShowing) return;
        if (Time.unscaledTime < hideAtTime) return;

        FinishCurrent();
    }

    public void Show(MonologueSO monologue, bool? queue = null)
    {
        if (monologue == null)
        {
            Debug.LogWarning("[MonologueManager] MonologueSO is null.", this);
            return;
        }

        Show(monologue.text, monologue.holdSeconds, queue);
    }

    public void Show(string text, float holdSeconds = -1f, bool? queue = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[MonologueManager] Text is empty.", this);
            return;
        }

        if (!EnsureView()) return;

        var request = new Request(text, holdSeconds);
        bool shouldQueue = queue ?? queueByDefault;

        if (isShowing && shouldQueue)
        {
            this.queue.Enqueue(request);
            return;
        }

        if (isShowing)
            view.Hide();

        Play(request);
    }

    public void Clear()
    {
        queue.Clear();
        isShowing = false;
        hideAtTime = 0f;

        if (view != null)
            view.Hide();
    }

    void Play(Request request)
    {
        current = request;
        isShowing = true;

        if (view != null)
            view.Show(request.Text);

        float typingSeconds = view != null ? view.GetTypingSeconds(request.Text) : 0f;
        hideAtTime = Time.unscaledTime + typingSeconds + ResolveHoldSeconds(request);
    }

    void FinishCurrent()
    {
        if (queue.Count > 0)
        {
            Play(queue.Dequeue());
            return;
        }

        isShowing = false;
        hideAtTime = 0f;
        view.Hide();
    }

    float ResolveHoldSeconds(Request request)
    {
        if (request.HoldSeconds >= 0f)
            return request.HoldSeconds;

        return defaultHoldSeconds + request.Text.Length * holdSecondsPerCharacter;
    }

    bool EnsureView()
    {
        if (view == null)
            view = FindAnyObjectByType<MonologueView>();

        if (view != null)
            return true;

        if (!warnedMissingView)
        {
            Debug.LogWarning("[MonologueManager] MonologueView is not found in the scene. Falling back to built-in IMGUI display.", this);
            warnedMissingView = true;
        }

        return true;
    }

    void OnGUI()
    {
        if (!isShowing || view != null) return;

        const float marginXRatio = 0.08f;
        const float bottomMargin = 42f;
        const float height = 96f;

        float x = Screen.width * marginXRatio;
        float width = Screen.width * (1f - marginXRatio * 2f);
        float y = Screen.height - bottomMargin - height;
        Rect rect = new Rect(x, y, width, height);

        GUI.Box(rect, GUIContent.none);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 25,
            wordWrap = true,
            normal = { textColor = new Color(0.92f, 0.9f, 0.85f) }
        };

        GUI.Label(new Rect(rect.x + 24f, rect.y + 12f, rect.width - 48f, rect.height - 24f), current.Text, style);
    }
}
