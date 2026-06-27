// StartMenuController.cs
// 기능 : 시작 화면 제어기. 좌측 하단 Start/Option/Exit 버튼을 처리하고, 옵션 팝업(OptionsPanel)을
//        런타임에 주입해 표시/숨김을 관리한다.
//        - Start  : SceneController 로 startTargetScene 씬 로드
//        - Option : 옵션 팝업 표시
//        - Exit   : 애플리케이션 종료(에디터에선 플레이 중지)
// 사용 : 시작 씬의 UIDocument(Source Asset = StartMenu.uxml) 가 붙은 GameObject 에 추가하고,
//        인스펙터에서 optionsPanelAsset(= OptionsPanel.uxml) 과 startTargetScene 을 지정한다.

using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class StartMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("시작 버튼을 눌렀을 때 SceneController 로 로드할 대상 씬 이름")]
    [SerializeField] private string startTargetScene;

    [Header("옵션 팝업")]
    [Tooltip("재사용할 옵션 팝업 UXML(OptionsPanel.uxml). 런타임에 복제되어 화면에 주입됩니다.")]
    [SerializeField] private VisualTreeAsset optionsPanelAsset;

    private const string StartButtonName = "btn-start";
    private const string OptionButtonName = "btn-option";
    private const string ExitButtonName = "btn-exit";

    private OptionsPanelController _options;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[StartMenuController] rootVisualElement 가 없습니다. Source Asset 을 확인하세요.", this);
            return;
        }

        SetupOptionsPanel(root);
        BindButtons(root);
    }

    /// <summary>옵션 팝업을 복제해 루트에 추가하고 컨트롤러를 생성한다. 초기엔 숨김.</summary>
    private void SetupOptionsPanel(VisualElement root)
    {
        if (optionsPanelAsset == null)
        {
            Debug.LogWarning("[StartMenuController] optionsPanelAsset 이 비어 있어 옵션 팝업을 사용할 수 없습니다.", this);
            return;
        }

        var panelRoot = optionsPanelAsset.CloneTree();
        // CloneTree 결과 컨테이너가 화면을 덮도록 stretch
        panelRoot.style.position = Position.Absolute;
        panelRoot.style.left = 0;
        panelRoot.style.top = 0;
        panelRoot.style.right = 0;
        panelRoot.style.bottom = 0;
        root.Add(panelRoot);

        _options = new OptionsPanelController(panelRoot);
        _options.Hide();
    }

    private void BindButtons(VisualElement root)
    {
        var startButton = root.Q<Button>(StartButtonName);
        if (startButton != null) startButton.clicked += OnStart;

        var optionButton = root.Q<Button>(OptionButtonName);
        if (optionButton != null) optionButton.clicked += OnOption;

        var exitButton = root.Q<Button>(ExitButtonName);
        if (exitButton != null) exitButton.clicked += OnExit;
    }

    private void OnStart()
    {
        if (string.IsNullOrEmpty(startTargetScene))
        {
            Debug.LogError("[StartMenuController] startTargetScene 이 비어 있습니다.", this);
            return;
        }
        SceneController.Instance.LoadScene(startTargetScene);
    }

    private void OnOption() => _options?.Show();

    private void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
