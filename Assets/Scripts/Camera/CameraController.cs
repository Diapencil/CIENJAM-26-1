// CameraController.cs
// 기능: 1인칭 시점 카메라와 카메라(뷰파인더) 모드 카메라를 전환한다. 카메라 모드 카메라가 달린
//       "카메라 오브젝트"의 자세를 main(fps) 카메라의 월드 트랜스폼 기준 오프셋으로 매 프레임 배치한다.
//       모드 전환은 off↔on 오프셋 사이를 DOTween(progress 0↔1)으로 보간해 들어올림/내림을 연출한다.
//       오프셋이 main 카메라 공간 기준이라 pitch·yaw·위치가 자동으로 따라온다.
//       촬영 서브모드에서는 마우스 휠로 cameraViewCamera 의 FOV(minFov~maxFov)를 조절해 확대/축소한다.
// 사용법: 인게임 씬의 빈 GameObject 에 본 컴포넌트를 붙이고 인스펙터에
//           firstPersonCamera : 1인칭 시점 카메라(Player_Ctrl 이 회전시키는 main 카메라)
//           cameraObject      : 트윈으로 배치할 카메라 오브젝트(부모는 무관 — 월드 자세를 직접 세팅)
//           cameraViewCamera  : TextureCam 하위의 CamViewCam(카메라 모드 카메라). GameObject 단위 SetActive 토글
//           off/on 오프셋(위치·회전, main 카메라 로컬 공간 기준), 트윈 시간/이즈
//         를 지정한다. 외부는
//           CameraController.Current.ActiveCamera / IsCameraView / OnModeChanged
//         로 접근한다.
//
// 입력 매핑(임시):
//   마우스 우클릭(탭)  → 1인칭 ↔ 카메라 모드 토글
//   Tab               → 카메라 모드 안에서 촬영 ↔ 앨범 서브모드 전환
// 서브모드(CurrentViewMode)는 인스턴스 필드라 카메라를 내렸다 다시 들어도 같은 세션 동안 유지된다.
// 앨범 서브모드 진입 시 Time.timeScale 을 0 으로(시간 정지), 이탈/모드 종료 시 1 로 복구한다.

using System;
using DG.Tweening;
using UnityEngine;

public enum CameraMode
{
    FirstPerson, // 1인칭 시점
    CameraView   // 카메라(뷰파인더) 모드
}

/// <summary>카메라 모드 안의 서브모드.</summary>
public enum CameraViewMode
{
    Shoot, // 촬영(플래시) 모드
    Album  // 앨범(사진 열람) 모드
}

public class CameraController : DomainSingleton<CameraController>
{
    [Header("Cameras")]
    [Tooltip("1인칭 시점에서 렌더링할 main 카메라(Player_Ctrl 이 회전)")]
    [SerializeField] private Camera firstPersonCamera;
    [Tooltip("자세를 배치할 카메라 오브젝트")]
    [SerializeField] private Transform cameraObject;
    [Tooltip("TextureCam 하위의 CamViewCam(카메라 모드 카메라). GameObject 단위로 SetActive 토글된다.")]
    [SerializeField] private Camera cameraViewCamera;

    [Header("Off/On 오프셋 (main 카메라 로컬 공간 기준)")]
    [Tooltip("카메라 off(1인칭) 상태의 위치 오프셋 (main 카메라 기준)")]
    [SerializeField] private Vector3 offPosition;
    [Tooltip("카메라 off 상태의 회전 오프셋(Euler, main 카메라 기준)")]
    [SerializeField] private Vector3 offEuler;
    [Tooltip("카메라 on(카메라 모드) 상태의 위치 오프셋 (main 카메라 기준)")]
    [SerializeField] private Vector3 onPosition;
    [Tooltip("카메라 on 상태의 회전 오프셋(Euler, main 카메라 기준)")]
    [SerializeField] private Vector3 onEuler;

    [Header("Tween")]
    [SerializeField] private float tweenDuration = 0.3f;
    [SerializeField] private Ease tweenEase = Ease.OutCubic;

    [Header("Zoom (확대) — 촬영 모드에서 마우스 휠로 조작")]
    [Tooltip("최대 확대 시 FOV(작을수록 더 확대). maxFov 이하로 설정")]
    [SerializeField] private float minFov = 20f;
    [Tooltip("기본(축소) 상태 FOV. 카메라 모드 진입 시 이 값으로 초기화")]
    [SerializeField] private float maxFov = 60f;
    [Tooltip("마우스 휠 1노치당 FOV 변화량(도)")]
    [SerializeField] private float zoomSpeed = 5f;
    [Tooltip("목표 FOV로 보간하는 속도. 0 이하면 즉시 적용")]
    [SerializeField] private float zoomLerpSpeed = 12f;

    [Header("Input (임시 매핑)")]
    [Tooltip("1인칭 ↔ 카메라 모드 토글에 사용할 마우스 버튼")]
    [SerializeField] private MouseButton switchButton = MouseButton.Right;
    [Tooltip("촬영 ↔ 앨범 서브모드 전환 키")]
    [SerializeField] private KeyCode viewModeSwitchKey = KeyCode.Tab;

    /// <summary>현재 카메라 모드. (정적 DomainSingleton.Current 와 구분하기 위해 CurrentMode)</summary>
    public CameraMode CurrentMode { get; private set; } = CameraMode.FirstPerson;

    /// <summary>카메라 모드 안의 서브모드(촬영/앨범). 같은 세션 동안 카메라 재진입 시 유지된다.</summary>
    public CameraViewMode CurrentViewMode { get; private set; } = CameraViewMode.Shoot;

    /// <summary>카메라(뷰파인더) 모드 여부.</summary>
    public bool IsCameraView => CurrentMode == CameraMode.CameraView;

    /// <summary>카메라 모드 전환 트윈이 진행 중인지 여부.</summary>
    public bool IsModeTransitioning => _transitioning;

    /// <summary>카메라 모드 + 앨범 서브모드 여부(시간 정지/앨범 표시 조건).</summary>
    public bool IsAlbumView => IsCameraView && CurrentViewMode == CameraViewMode.Album;

    /// <summary>현재 활성(렌더 중) 카메라.</summary>
    public Camera ActiveCamera => IsCameraView ? cameraViewCamera : firstPersonCamera;

    /// <summary>모드가 바뀔 때 발행된다.</summary>
    public event Action<CameraMode> OnModeChanged;

    /// <summary>서브모드(촬영/앨범)가 바뀔 때 발행된다.</summary>
    public event Action<CameraViewMode> OnViewModeChanged;

    /// <summary>모드 전환 트윈(들어올림/내림)이 끝났을 때 발행된다. UI 표시 타이밍 동기화용.</summary>
    public event Action<CameraMode> OnModeTransitionComplete;

    private Tween _tween;
    private bool _transitioning;  // 트윈 진행 중 여부
    private float _progress;      // 0 = off, 1 = on
    private float _targetFov;     // 줌 목표 FOV(휠 입력 누적)

    private void Start()
    {
        ValidateRefs();
        InitFirstPerson();
    }

    private void ValidateRefs()
    {
        if (firstPersonCamera == null)
            Debug.LogWarning("[CameraController] firstPersonCamera 미할당 — 자세 배치/렌더 토글이 동작하지 않습니다.", this);
        if (cameraObject == null)
            Debug.LogWarning("[CameraController] cameraObject 미할당 — 카메라 모드 전환이 동작하지 않습니다.", this);
        else if (!cameraObject.gameObject.activeInHierarchy)
            Debug.LogWarning("[CameraController] cameraObject 의 GameObject 가 비활성입니다. 카메라 모드 카메라가 렌더되지 않으니 활성 상태로 두고 Camera.enabled 로만 토글하세요.", this);
        if (cameraViewCamera == null)
            Debug.LogWarning("[CameraController] cameraViewCamera 미할당 — 카메라 모드 카메라가 활성화되지 않습니다.", this);
    }

    private void OnEnable()
    {
        UserInput.Instance.AddMouseListener(switchButton, KeyPhase.Down, ToggleMode);
        UserInput.Instance.AddKeyListener(viewModeSwitchKey, KeyPhase.Down, OnSwitchViewMode);
    }

    private void OnDisable()
    {
        if (UserInput.Instance != null)
        {
            UserInput.Instance.RemoveMouseListener(switchButton, KeyPhase.Down, ToggleMode);
            UserInput.Instance.RemoveKeyListener(viewModeSwitchKey, KeyPhase.Down, OnSwitchViewMode);
        }
        Time.timeScale = 1f; // 앨범 모드 중 비활성화되어도 시간 정지가 남지 않도록 복구
    }

    // Tab: 카메라 모드 안에서만 촬영 ↔ 앨범 서브모드 전환.
    private void OnSwitchViewMode()
    {
        if (!IsCameraView) return;
        SetViewMode(CurrentViewMode == CameraViewMode.Shoot ? CameraViewMode.Album : CameraViewMode.Shoot);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _tween?.Kill();
    }

    private void Update()
    {
        UpdateZoom();
    }

    // main 카메라가 Player_Ctrl.Update 에서 회전/이동을 마친 뒤, 그 월드 트랜스폼 기준으로 자세를 배치한다.
    private void LateUpdate()
    {
        ApplyPose();
    }

    // 촬영 서브모드에서만 마우스 휠로 cameraViewCamera 의 FOV 를 조절해 확대/축소한다.
    // 휠 위(+)=확대(FOV 감소), 아래(-)=축소(FOV 증가). minFov~maxFov 로 클램프.
    private void UpdateZoom()
    {
        if (cameraViewCamera == null) return;
        if (!IsCameraView || CurrentViewMode != CameraViewMode.Shoot) return;

        float scroll = UserInput.Instance.ScrollDelta.y;
        if (!Mathf.Approximately(scroll, 0f))
            _targetFov = Mathf.Clamp(_targetFov - scroll * zoomSpeed, minFov, maxFov);

        float current = cameraViewCamera.fieldOfView;
        cameraViewCamera.fieldOfView = zoomLerpSpeed > 0f
            ? Mathf.Lerp(current, _targetFov, Time.unscaledDeltaTime * zoomLerpSpeed)
            : _targetFov;
    }

    /// <summary>1인칭 ↔ 카메라 모드 토글.</summary>
    public void ToggleMode() => SetMode(IsCameraView ? CameraMode.FirstPerson : CameraMode.CameraView);

    /// <summary>지정한 모드로 전환하고 트윈 연출을 재생한다.</summary>
    public void SetMode(CameraMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        if (mode == CameraMode.CameraView) ResetZoom(); // 진입 시 기본 배율로 시작
        PlayTransition(mode);
        OnModeChanged?.Invoke(mode);
        UpdateTimeScale();
    }

    /// <summary>촬영/앨범 서브모드를 전환한다.</summary>
    public void SetViewMode(CameraViewMode mode)
    {
        if (CurrentViewMode == mode) return;
        CurrentViewMode = mode;
        OnViewModeChanged?.Invoke(mode);
        UpdateTimeScale();
    }

    // 앨범 서브모드(카메라 모드 + Album)일 때만 시간을 정지한다.
    private void UpdateTimeScale()
    {
        Time.timeScale = IsAlbumView ? 0f : 1f;
    }

    private void InitFirstPerson()
    {
        CurrentMode = CameraMode.FirstPerson;
        _transitioning = false;
        _progress = 0f;
        if (cameraViewCamera != null) cameraViewCamera.gameObject.SetActive(false);
        if (firstPersonCamera != null) firstPersonCamera.enabled = true;
        ResetZoom();
        ApplyPose();
    }

    // 줌 목표·현재 FOV 를 기본(축소) 배율로 되돌린다.
    private void ResetZoom()
    {
        _targetFov = maxFov;
        if (cameraViewCamera != null) cameraViewCamera.fieldOfView = maxFov;
    }

    private void PlayTransition(CameraMode mode)
    {
        _tween?.Kill();
        _transitioning = true;

        // 하강(1인칭 복귀): 즉시 fps 로 렌더 전환 후, 카메라 모델이 내려가는 동작을 fps 화면으로 보여준다.
        if (mode == CameraMode.FirstPerson)
        {
            if (firstPersonCamera != null) firstPersonCamera.enabled = true;
            if (cameraViewCamera != null) cameraViewCamera.gameObject.SetActive(false);
        }

        float target = mode == CameraMode.CameraView ? 1f : 0f;
        _tween = DOTween.To(() => _progress, v => _progress = v, target, tweenDuration)
            .SetEase(tweenEase)
            .SetUpdate(true) // 앨범 모드(timeScale 0)에서도 전환 연출이 멈추지 않도록 언스케일드 타임 사용
            .OnComplete(() =>
            {
                // 상승 완료 시점에 카메라 모드로 렌더 전환(들어올림은 fps 화면으로 보여줬으므로).
                if (mode == CameraMode.CameraView)
                {
                    if (cameraViewCamera != null) cameraViewCamera.gameObject.SetActive(true);
                    if (firstPersonCamera != null) firstPersonCamera.enabled = false;
                }
                _transitioning = false;
                OnModeTransitionComplete?.Invoke(mode);
            });
    }

    // off↔on 오프셋을 progress 로 보간해 main 카메라 월드 트랜스폼 기준으로 cameraObject 자세를 세팅한다.
    private void ApplyPose()
    {
        if (cameraObject == null || firstPersonCamera == null) return;

        Transform cam = firstPersonCamera.transform;
        Vector3 posOffset = Vector3.Lerp(offPosition, onPosition, _progress);
        Quaternion rotOffset = Quaternion.Slerp(Quaternion.Euler(offEuler), Quaternion.Euler(onEuler), _progress);

        cameraObject.SetPositionAndRotation(cam.TransformPoint(posOffset), cam.rotation * rotOffset);
    }
}
