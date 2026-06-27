// DeathPanelController.cs
// Feature: Controls the death result panel and routes Restart/Menu buttons to GameManager.
// Usage: Attach to a UIDocument using DeathPanel.uxml. Required element names:
//        death-root, death-reason, death-restart, death-menu.

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

[RequireComponent(typeof(UIDocument))]
public class DeathPanelController : MonoBehaviour
{
    private const string RootName = "death-root";
    private const string ReasonName = "death-reason";
    private const string RestartName = "death-restart";
    private const string MenuName = "death-menu";

    [Header("Fallback Scenes")]
    [SerializeField] private string restartSceneName = "Play";
    [SerializeField] private string menuSceneName = "Start";

    private VisualElement _root;
    private UIDocument _document;
    private VisualElement _documentRoot;
    private Label _reasonLabel;
    private Button _restartButton;
    private Button _menuButton;
    private bool _actionSelected;

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
        if (_restartButton != null)
            _restartButton.clicked -= Restart;

        if (_menuButton != null)
            _menuButton.clicked -= ReturnToMenu;

        CursorStateController.Release(this);
    }

    private void Bind()
    {
        _document = GetComponent<UIDocument>();
        _documentRoot = _document != null ? _document.rootVisualElement : null;
        if (_documentRoot == null)
        {
            Debug.LogWarning("[DeathPanelController] UIDocument rootVisualElement is null. Cannot bind death panel yet.", this);
            return;
        }

        _root = _documentRoot.Q<VisualElement>(RootName) ?? _documentRoot;
        _reasonLabel = _documentRoot.Q<Label>(ReasonName);

        if (_restartButton == null)
        {
            _restartButton = _documentRoot.Q<Button>(RestartName);
            if (_restartButton != null)
                _restartButton.clicked += Restart;
        }

        if (_menuButton == null)
        {
            _menuButton = _documentRoot.Q<Button>(MenuName);
            if (_menuButton != null)
                _menuButton.clicked += ReturnToMenu;
        }
    }

    public void Show(GameManager.DeathContext context)
    {
        Bind();

        if (_document != null)
            _document.enabled = true;

        if (_documentRoot != null)
        {
            _documentRoot.style.display = DisplayStyle.Flex;
            _documentRoot.style.position = Position.Absolute;
            _documentRoot.style.left = 0;
            _documentRoot.style.top = 0;
            _documentRoot.style.right = 0;
            _documentRoot.style.bottom = 0;
        }

        if (_reasonLabel != null)
            _reasonLabel.text = string.IsNullOrWhiteSpace(context.Reason) ? "You died." : context.Reason;

        _actionSelected = false;
        SetButtonsEnabled(true);
        CursorStateController.RequestUnlocked(this);

        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.style.visibility = Visibility.Visible;
            _root.style.opacity = 1f;
            _root.pickingMode = PickingMode.Position;
        }
        else
        {
            Debug.LogWarning("[DeathPanelController] death-root was not found and document root is unavailable.", this);
        }

        Debug.Log($"[DeathPanelController] Show death panel. reason='{context.Reason}' source='{context.Source}'", this);
        StartCoroutine(LogLayoutNextFrame());
    }

    public void Hide()
    {
        if (_root != null)
            _root.style.display = DisplayStyle.None;

        _actionSelected = false;
        SetButtonsEnabled(false);
        CursorStateController.Release(this);
    }

    private IEnumerator LogLayoutNextFrame()
    {
        yield return null;

        if (_root == null)
        {
            Debug.LogWarning("[DeathPanelController] Layout check failed: root is null.", this);
            yield break;
        }

        Debug.Log(
            $"[DeathPanelController] Layout check. display={_root.resolvedStyle.display} visibility={_root.resolvedStyle.visibility} opacity={_root.resolvedStyle.opacity:0.###} width={_root.resolvedStyle.width:0.###} height={_root.resolvedStyle.height:0.###}",
            this);
    }

    private void Restart()
    {
        if (_actionSelected) return;
        _actionSelected = true;
        SetButtonsEnabled(false);

        Debug.Log("[DeathPanelController] Restart selected.", this);
        if (GameManager.Current != null)
        {
            GameManager.Current.RestartGame();
            return;
        }

        LoadFallbackScene(restartSceneName);
    }

    private void ReturnToMenu()
    {
        if (_actionSelected) return;
        _actionSelected = true;
        SetButtonsEnabled(false);

        Debug.Log("[DeathPanelController] Menu selected.", this);
        if (GameManager.Current != null)
        {
            GameManager.Current.ReturnToTitle();
            return;
        }

        LoadFallbackScene(menuSceneName);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _restartButton?.SetEnabled(enabled);
        _menuButton?.SetEnabled(enabled);
    }

    private void LoadFallbackScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[DeathPanelController] Fallback scene name is empty.", this);
            return;
        }

        if (SceneController.Instance == null)
        {
            Debug.LogError("[DeathPanelController] SceneController.Instance is missing; cannot load scene.", this);
            return;
        }

        SceneController.Instance.LoadScene(sceneName);
    }

}
