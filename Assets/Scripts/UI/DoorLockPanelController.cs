// DoorLockPanelController.cs
// Feature: Door-lock keypad UI. Shows a clickable numeric pad, displays the pressed digits,
//          validates a fixed-length password, and reports success via a callback.
// Usage: Attach to a UIDocument using DoorLock.uxml. Required element names:
//        lock-root, lock-display, lock-key-0..lock-key-9, lock-delete, lock-confirm.
//        Call Open(correctPassword, onSuccess) to show it (DoorLockKeypad does this).
//        Input is committed once opened (no cancel); the cursor is unlocked while open.

using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DoorLockPanelController : MonoBehaviour
{
    private const string RootName = "lock-root";
    private const string DisplayName = "lock-display";
    private const string DeleteName = "lock-delete";
    private const string ConfirmName = "lock-confirm";
    private const string WrongClass = "lock-display-wrong";

    [SerializeField, Min(0.1f)] private float wrongFeedbackSeconds = 0.6f;

    private UIDocument _document;
    private VisualElement _documentRoot;
    private VisualElement _root;
    private Label _display;
    private Button[] _digitButtons = new Button[10];
    private Button _deleteButton;
    private Button _confirmButton;
    private bool _bound;

    private readonly StringBuilder _input = new();
    private string _correct;
    private Action _onSuccess;
    private bool _open;

    private void Awake()
    {
        Bind();
        Hide();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        UnwireButtons();
        CursorStateController.Release(this);
        CameraLookLock.Release(this);
    }

    private void Bind()
    {
        if (_bound) return;

        _document = GetComponent<UIDocument>();
        _documentRoot = _document != null ? _document.rootVisualElement : null;
        if (_documentRoot == null)
        {
            Debug.LogWarning("[DoorLockPanelController] UIDocument rootVisualElement is null. Cannot bind yet.", this);
            return;
        }

        _root = _documentRoot.Q<VisualElement>(RootName) ?? _documentRoot;
        _display = _documentRoot.Q<Label>(DisplayName);

        for (int i = 0; i <= 9; i++)
        {
            int digit = i;
            Button b = _documentRoot.Q<Button>($"lock-key-{i}");
            _digitButtons[i] = b;
            if (b != null) b.clicked += () => OnDigit(digit);
        }

        _deleteButton = _documentRoot.Q<Button>(DeleteName);
        if (_deleteButton != null) _deleteButton.clicked += OnDelete;

        _confirmButton = _documentRoot.Q<Button>(ConfirmName);
        if (_confirmButton != null) _confirmButton.clicked += OnConfirm;

        _bound = true;
    }

    private void UnwireButtons()
    {
        // UIToolkit Button.clicked uses anonymous delegates here; rebinding is guarded by _bound.
        // Releasing the document on disable is enough for this single-scene panel.
        _bound = false;
    }

    /// <summary>키패드를 열고 입력을 시작한다. correctPassword 와 일치 시 onSuccess 호출.</summary>
    public void Open(string correctPassword, Action onSuccess)
    {
        Bind();

        _correct = correctPassword ?? string.Empty;
        _onSuccess = onSuccess;
        _input.Clear();
        UpdateDisplay();
        _open = true;

        if (_document != null) _document.enabled = true;

        if (_documentRoot != null)
        {
            _documentRoot.style.display = DisplayStyle.Flex;
            _documentRoot.style.position = Position.Absolute;
            _documentRoot.style.left = 0;
            _documentRoot.style.top = 0;
            _documentRoot.style.right = 0;
            _documentRoot.style.bottom = 0;
        }

        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.style.visibility = Visibility.Visible;
            _root.pickingMode = PickingMode.Position;
        }

        CursorStateController.RequestUnlocked(this);
        CameraLookLock.RequestLocked(this);
        Debug.Log($"[DoorLockPanelController] Keypad opened. length={_correct.Length}", this);
    }

    public void Hide()
    {
        _open = false;
        if (_root != null) _root.style.display = DisplayStyle.None;
        CursorStateController.Release(this);
        CameraLookLock.Release(this);
    }

    private void OnDigit(int digit)
    {
        if (!_open) return;
        if (_input.Length >= _correct.Length) return;
        _input.Append(digit);
        UpdateDisplay();
    }

    private void OnDelete()
    {
        if (!_open || _input.Length == 0) return;
        _input.Remove(_input.Length - 1, 1);
        UpdateDisplay();
    }

    private void OnConfirm()
    {
        if (!_open) return;

        if (_input.ToString() == _correct && _correct.Length > 0)
        {
            Debug.Log("[DoorLockPanelController] Correct password. Unlocking.", this);
            _open = false;
            Action cb = _onSuccess;
            Hide();
            cb?.Invoke();
            return;
        }

        Debug.Log($"[DoorLockPanelController] Wrong password entered: '{_input}'", this);
        _input.Clear();
        UpdateDisplay();
        FlashWrong();
    }

    private void UpdateDisplay()
    {
        if (_display != null) _display.text = _input.ToString();
    }

    private void FlashWrong()
    {
        if (_display == null) return;
        _display.AddToClassList(WrongClass);
        _display.schedule.Execute(() => _display.RemoveFromClassList(WrongClass))
                         .StartingIn((long)(wrongFeedbackSeconds * 1000f));
    }
}
