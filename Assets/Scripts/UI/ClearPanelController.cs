// ClearPanelController.cs
// Feature: Controls the clear/escape result panel and routes Restart/Menu buttons to GameManager.
// Usage: Attach to a UIDocument using ClearPanel.uxml. Required element names:
//        clear-root, clear-restart, clear-menu. ClearSequenceController calls Show().

using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ClearPanelController : MonoBehaviour
{
    private const string RootName = "clear-root";
    private const string RestartName = "clear-restart";
    private const string MenuName = "clear-menu";

    [Header("Fallback Scenes")]
    [SerializeField] private string restartSceneName = "Play";
    [SerializeField] private string menuSceneName = "Start";

    private UIDocument _document;
    private VisualElement _documentRoot;
    private VisualElement _root;
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
        if (_restartButton != null) _restartButton.clicked -= Restart;
        if (_menuButton != null) _menuButton.clicked -= ReturnToMenu;
        CursorStateController.Release(this);
    }

    private void Bind()
    {
        _document = GetComponent<UIDocument>();
        _documentRoot = _document != null ? _document.rootVisualElement : null;
        if (_documentRoot == null)
        {
            Debug.LogWarning("[ClearPanelController] UIDocument rootVisualElement is null. Cannot bind clear panel yet.", this);
            return;
        }

        _root = _documentRoot.Q<VisualElement>(RootName) ?? _documentRoot;

        if (_restartButton == null)
        {
            _restartButton = _documentRoot.Q<Button>(RestartName);
            if (_restartButton != null) _restartButton.clicked += Restart;
        }

        if (_menuButton == null)
        {
            _menuButton = _documentRoot.Q<Button>(MenuName);
            if (_menuButton != null) _menuButton.clicked += ReturnToMenu;
        }
    }

    public void Show()
    {
        Bind();

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

        Debug.Log("[ClearPanelController] Show clear panel.", this);
    }

    public void Hide()
    {
        if (_root != null) _root.style.display = DisplayStyle.None;
        _actionSelected = false;
        SetButtonsEnabled(false);
        CursorStateController.Release(this);
    }

    private void Restart()
    {
        if (_actionSelected) return;
        _actionSelected = true;
        SetButtonsEnabled(false);

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
            Debug.LogError("[ClearPanelController] Fallback scene name is empty.", this);
            return;
        }
        if (SceneController.Instance == null)
        {
            Debug.LogError("[ClearPanelController] SceneController.Instance is missing; cannot load scene.", this);
            return;
        }
        SceneController.Instance.LoadScene(sceneName);
    }
}
