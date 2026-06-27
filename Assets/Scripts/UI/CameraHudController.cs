// CameraHudController.cs
// 기능 : 카메라 모드 HUD(CameraHUD.uxml) 제어기. CameraSystem 의 배터리 변화 이벤트를 구독해
//        배터리 칸 수(숫자)를 갱신하고, CameraController 의 모드 변화에 따라 HUD 표시를 토글한다.
// 사용 : CameraHUD.uxml 을 Source Asset 으로 가진 UIDocument GameObject 에 본 컴포넌트를 붙인다.
//        씬 안의 CameraSystem / CameraController 를 런타임에 찾아 자동 연결한다.
//        (배터리는 현재 숫자 표시이며 추후 그래픽으로 교체 예정)

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CameraHudController : MonoBehaviour
{
    // UXML 요소 이름 (CameraHUD.uxml 과 일치해야 함)
    private const string RootName = "camera-hud-root";
    private const string NormalLabelName = "battery-normal";
    private const string ChargeLabelName = "battery-charge";

    private VisualElement _root;
    private Label _normalLabel;
    private Label _chargeLabel;

    private CameraSystem _cameraSystem;
    private CameraController _controller;

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (_cameraSystem != null)
        {
            _cameraSystem.OnNormalBatteryChanged -= UpdateNormal;
            _cameraSystem.OnChargeBatteryChanged -= UpdateCharge;
        }
        if (_controller != null)
            _controller.OnModeChanged -= UpdateVisibility;
    }

    private IEnumerator BindWhenReady()
    {
        yield return null; // UIDocument rootVisualElement 준비 대기

        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[CameraHudController] UIDocument 의 rootVisualElement 가 없습니다. Source Asset 확인 필요.", this);
            yield break;
        }

        _root = root.Q<VisualElement>(RootName);
        _normalLabel = root.Q<Label>(NormalLabelName);
        _chargeLabel = root.Q<Label>(ChargeLabelName);

        _cameraSystem = FindAnyObjectByType<CameraSystem>();
        if (_cameraSystem != null)
        {
            _cameraSystem.OnNormalBatteryChanged += UpdateNormal;
            _cameraSystem.OnChargeBatteryChanged += UpdateCharge;
            UpdateNormal(_cameraSystem.NormalSlots, _cameraSystem.NormalMax);
            UpdateCharge(_cameraSystem.ChargeSlots, _cameraSystem.ChargeMax);
        }
        else
        {
            Debug.LogWarning("[CameraHudController] 씬에서 CameraSystem 을 찾지 못했습니다.", this);
        }

        _controller = CameraController.Current;
        if (_controller != null)
        {
            _controller.OnModeChanged += UpdateVisibility;
            UpdateVisibility(_controller.CurrentMode);
        }
    }

    private void UpdateNormal(int current, int max)
    {
        if (_normalLabel != null) _normalLabel.text = $"일반 {current}/{max}";
    }

    private void UpdateCharge(int current, int max)
    {
        if (_chargeLabel != null) _chargeLabel.text = $"차지 {current}/{max}";
    }

    // 카메라 모드일 때만 HUD 를 표시한다.
    private void UpdateVisibility(CameraMode mode)
    {
        if (_root == null) return;
        _root.style.display = mode == CameraMode.CameraView ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
