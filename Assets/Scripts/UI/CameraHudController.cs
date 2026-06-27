// CameraHudController.cs
// 기능 : 카메라 모드 HUD(CameraHUD.uxml) 제어기.
//        - CameraSystem 의 배터리 변화 이벤트를 구독해, 일반 배터리는 잔량 칸 수에 해당하는
//          Image 1장만 활성화(겹쳐 둔 0~N 잔량 이미지 중 택1)하고, 고성능(차지) 배터리는
//          번개 Image 의 활성/비활성으로 표시한다.
//        - PhotoAlbum 을 구독해 좌하단에 사진 저장 용량(현재/최대)을 갱신한다.
//        - HUD 표시는 카메라 진입 시 전환 트윈 완료(OnModeTransitionComplete) 시점에 켜고,
//          1인칭 복귀 시작 시(OnModeChanged) 즉시 끈다.
// 사용 : CameraHUD.uxml 을 Source Asset 으로 가진 UIDocument GameObject 에 본 컴포넌트를 붙인다.
//        씬 안의 CameraSystem / CameraController / PhotoAlbum 을 런타임에 찾아 자동 연결한다.
//        잔량 이미지는 UXML 에서 battery-normal-0, battery-normal-1 ... 순으로 두면 자동 인식한다.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CameraHudController : MonoBehaviour
{
    // UXML 요소 이름 (CameraHUD.uxml 과 일치해야 함)
    private const string RootName = "camera-hud-root";
    private const string NormalLevelPrefix = "battery-normal-"; // 뒤에 잔량 인덱스(0,1,2...)
    private const string ChargeImageName = "battery-charge";
    private const string StorageLabelName = "photo-storage";

    [Header("사진 저장 용량")]
    [Tooltip("좌하단에 표시할 사진 최대 저장 칸 수(분모)")]
    [SerializeField] private int photoCapacity = 24;

    private VisualElement _root;
    private readonly List<Image> _normalLevels = new(); // 인덱스 = 잔량 칸 수
    private Image _chargeImage;
    private Label _storageLabel;

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
        {
            _controller.OnModeChanged -= OnModeChanged;
            _controller.OnViewModeChanged -= OnViewModeChanged;
            _controller.OnModeTransitionComplete -= OnTransitionComplete;
        }
        if (PhotoAlbum.Current != null)
            PhotoAlbum.Current.OnPhotoAdded -= OnPhotoAdded;
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

        // 잔량 이미지(battery-normal-0, 1, 2 ...)를 끊기는 곳까지 순서대로 수집
        _normalLevels.Clear();
        for (int i = 0; ; i++)
        {
            var img = root.Q<Image>(NormalLevelPrefix + i);
            if (img == null) break;
            _normalLevels.Add(img);
        }
        if (_normalLevels.Count == 0)
            Debug.LogWarning("[CameraHudController] 일반 배터리 잔량 이미지(battery-normal-N)를 찾지 못했습니다.", this);

        _chargeImage = root.Q<Image>(ChargeImageName);
        _storageLabel = root.Q<Label>(StorageLabelName);

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

        if (PhotoAlbum.Current != null)
        {
            PhotoAlbum.Current.OnPhotoAdded += OnPhotoAdded;
            UpdateStorage(PhotoAlbum.Current.Photos.Count);
        }
        else
        {
            UpdateStorage(0);
        }

        _controller = CameraController.Current;
        if (_controller != null)
        {
            _controller.OnModeChanged += OnModeChanged;
            _controller.OnViewModeChanged += OnViewModeChanged;
            _controller.OnModeTransitionComplete += OnTransitionComplete;
            UpdateVisibility(); // 바인드 시점 현재 상태 반영
        }
    }

    // 일반 배터리: 잔량 칸 수에 해당하는 이미지 1장만 활성화, 나머지는 숨김.
    private void UpdateNormal(int current, int max)
    {
        if (_normalLevels.Count == 0) return;

        int idx = Mathf.Clamp(current, 0, _normalLevels.Count - 1);
        for (int i = 0; i < _normalLevels.Count; i++)
            _normalLevels[i].style.display = (i == idx) ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // 고성능(차지) 배터리: 잔량 1 이상이면 번개 이미지 활성, 0이면 비활성.
    private void UpdateCharge(int current, int max)
    {
        if (_chargeImage == null) return;
        _chargeImage.style.display = current > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnPhotoAdded(PhotoAlbum.Photo _)
    {
        UpdateStorage(PhotoAlbum.Current != null ? PhotoAlbum.Current.Photos.Count : 0);
    }

    private void UpdateStorage(int count)
    {
        if (_storageLabel != null) _storageLabel.text = $"{count}/{photoCapacity}";
    }

    // 카메라 모드를 나갈 때(1인칭 복귀)는 전환 시작 즉시 HUD 를 숨긴다.
    // 들어갈 때는 여기서 표시하지 않고, 전환 트윈 완료 시점(OnTransitionComplete)에 표시한다.
    private void OnModeChanged(CameraMode mode)
    {
        SetVisible(false);
    }

    private void OnViewModeChanged(CameraViewMode _)
    {
        UpdateVisibility();
    }

    // 들어올림 트윈이 끝난 뒤(카메라 모드 렌더 전환 완료) HUD 를 표시한다.
    private void OnTransitionComplete(CameraMode mode)
    {
        if (mode == CameraMode.CameraView) UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        SetVisible(_controller != null
                   && _controller.IsCameraView
                   && !_controller.IsModeTransitioning
                   && !_controller.IsAlbumView);
    }

    private void SetVisible(bool visible)
    {
        if (_root == null) return;
        _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
