// PauseMenuController.cs
// 기능: ESC 로 옵션(일시정지) UI 를 토글한다. 열릴 때 시간 정지(Time.timeScale=0),
//        커서 락 해제(CursorStateController), 마우스 시점 회전 차단(CameraLookLock) 을 함께 적용하고,
//        ESC 재입력 또는 옵션 패널의 닫기 버튼으로 원상 복구한다.
//        옵션 패널 자체는 기존 OptionsPanel.uxml + OptionsPanelController 를 재사용한다.
// 사용법: 인게임 씬에서 UIDocument 를 가진 GameObject 에 본 컴포넌트를 붙이고
//          optionsPanelAsset 에 OptionsPanel.uxml 을 할당한다. (PanelSettings 는 UIDocument 에 지정)
//        외부 호출 없이 ESC 입력만으로 동작한다.

using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    [Header("Options")]
    [Tooltip("재사용 OptionsPanel.uxml. 런타임에 이 문서로 CloneTree 된다.")]
    [SerializeField] private VisualTreeAsset optionsPanelAsset;

    [Tooltip("일시정지 토글 키")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    private VisualElement _optionsPanelRoot;
    private OptionsPanelController _options;
    private bool _paused;

    private void OnEnable()
    {
        var documentRoot = GetComponent<UIDocument>().rootVisualElement;
        if (documentRoot == null)
        {
            Debug.LogError("[PauseMenuController] UIDocument rootVisualElement 가 없습니다.", this);
            return;
        }

        SetupOptionsPanel(documentRoot);
        UserInput.Instance.AddKeyListener(toggleKey, KeyPhase.Down, Toggle);
    }

    private void OnDisable()
    {
        if (UserInput.Instance != null)
            UserInput.Instance.RemoveKeyListener(toggleKey, KeyPhase.Down, Toggle);

        // 일시정지 중 비활성화되어도 상태가 남지 않도록 복구한다.
        if (_paused) RestoreGameplay();

        _optionsPanelRoot?.RemoveFromHierarchy();
        _optionsPanelRoot = null;
        _options = null;
    }

    private void Update()
    {
        // 옵션 패널의 닫기 버튼은 내부에서 Hide() 만 호출하므로, 패널이 숨겨지면 게임 상태도 복구한다.
        if (_paused && _options != null && !_options.IsVisible)
            Close();
    }

    private void SetupOptionsPanel(VisualElement root)
    {
        if (optionsPanelAsset == null)
        {
            Debug.LogWarning("[PauseMenuController] optionsPanelAsset 미할당 — 옵션 팝업을 표시할 수 없습니다.", this);
            return;
        }

        _optionsPanelRoot = optionsPanelAsset.CloneTree();
        _optionsPanelRoot.style.position = Position.Absolute;
        _optionsPanelRoot.style.left = 0;
        _optionsPanelRoot.style.top = 0;
        _optionsPanelRoot.style.right = 0;
        _optionsPanelRoot.style.bottom = 0;
        root.Add(_optionsPanelRoot);

        _options = new OptionsPanelController(_optionsPanelRoot);
        _options.Hide();
    }

    /// <summary>ESC 토글: 열려 있으면 닫고, 닫혀 있으면 연다.</summary>
    private void Toggle()
    {
        if (_paused) Close();
        else Open();
    }

    private void Open()
    {
        if (_paused || _options == null) return;

        _paused = true;
        _options.Show();

        Time.timeScale = 0f;
        CursorStateController.RequestUnlocked(this);
        CameraLookLock.RequestLocked(this);
    }

    private void Close()
    {
        if (!_paused) return;

        _options?.Hide();
        RestoreGameplay();
    }

    private void RestoreGameplay()
    {
        _paused = false;
        Time.timeScale = 1f;
        CursorStateController.Release(this);
        CameraLookLock.Release(this);
    }
}