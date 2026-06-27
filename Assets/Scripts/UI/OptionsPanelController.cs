// OptionsPanelController.cs
// 기능 : 옵션 팝업의 동작 제어기(재사용 단위). 주어진 팝업 VisualElement 의 볼륨 슬라이더를
//        AudioManager 와 양방향 연결하고, Show()/Hide() 로 표시 상태를 토글한다.
//        MonoBehaviour 가 아니므로 시작 화면·인게임 어디서든 동일하게 인스턴스화해 재사용한다.
// 사용 : VisualTreeAsset.CloneTree() 로 OptionsPanel.uxml 을 복제해 UIDocument 루트에 추가한 뒤,
//          var options = new OptionsPanelController(clonedRoot);
//          options.Hide();           // 초기엔 숨김
//          options.Show();           // 옵션 버튼 등에서 호출
//        닫기 버튼은 내부에서 자동으로 Hide() 에 연결된다.

using UnityEngine;
using UnityEngine.UIElements;

public class OptionsPanelController
{
    // OptionsPanel.uxml 의 요소 이름과 일치해야 함
    private const string OverlayName = "options-overlay";
    private const string MasterSliderName = "slider-master";
    private const string SfxSliderName = "slider-sfx";
    private const string BgmSliderName = "slider-bgm";
    private const string CloseButtonName = "options-close";

    private readonly VisualElement _overlay;

    public bool IsVisible => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

    /// <param name="panelRoot">CloneTree 로 복제된 OptionsPanel 의 루트(또는 그 상위) VisualElement</param>
    public OptionsPanelController(VisualElement panelRoot)
    {
        if (panelRoot == null)
        {
            Debug.LogError("[OptionsPanelController] panelRoot 가 null 입니다.");
            return;
        }

        _overlay = panelRoot.Q<VisualElement>(OverlayName) ?? panelRoot;

        BindSlider(panelRoot, MasterSliderName,
            () => AudioManager.Instance.MasterVolume,
            v => AudioManager.Instance.MasterVolume = v);

        BindSlider(panelRoot, SfxSliderName,
            () => AudioManager.Instance.SFXVolume,
            v => AudioManager.Instance.SFXVolume = v);

        BindSlider(panelRoot, BgmSliderName,
            () => AudioManager.Instance.BGMVolume,
            v => AudioManager.Instance.BGMVolume = v);

        var closeButton = panelRoot.Q<Button>(CloseButtonName);
        if (closeButton != null)
            closeButton.clicked += Hide;
    }

    /// <summary>슬라이더 초기값을 getter 로 채우고, 값 변경 시 setter 로 즉시 반영한다.</summary>
    private void BindSlider(VisualElement root, string name, System.Func<float> getter, System.Action<float> setter)
    {
        var slider = root.Q<Slider>(name);
        if (slider == null)
        {
            Debug.LogWarning($"[OptionsPanelController] 슬라이더 '{name}' 를 찾을 수 없습니다.");
            return;
        }

        slider.SetValueWithoutNotify(getter());
        slider.RegisterValueChangedCallback(evt => setter(evt.newValue));
    }

    public void Show()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;
    }
}
