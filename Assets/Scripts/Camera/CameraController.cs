// CameraController.cs
// 기능: 1인칭 시점 카메라와 카메라(뷰파인더) 모드 카메라를 전환한다. 카메라 모드 카메라가 달린
//       "카메라 오브젝트"의 자세를 main(fps) 카메라의 월드 트랜스폼 기준 오프셋으로 매 프레임 배치한다.
//       모드 전환은 off↔on 오프셋 사이를 DOTween(progress 0↔1)으로 보간해 들어올림/내림을 연출한다.
//       오프셋이 main 카메라 공간 기준이라 pitch·yaw·위치가 자동으로 따라온다.
// 사용법: 인게임 씬의 빈 GameObject 에 본 컴포넌트를 붙이고 인스펙터에
//           firstPersonCamera : 1인칭 시점 카메라(Player_Ctrl 이 회전시키는 main 카메라)
//           cameraObject      : 트윈으로 배치할 카메라 오브젝트(부모는 무관 — 월드 자세를 직접 세팅)
//           cameraViewCamera  : TextureCam 하위의 CamViewCam(카메라 모드 카메라). GameObject 단위 SetActive 토글
//           off/on 오프셋(위치·회전, main 카메라 로컬 공간 기준), 트윈 시간/이즈
//         를 지정한다. 외부는
//           CameraController.Current.ActiveCamera / IsCameraView / OnModeChanged
//         로 접근한다. 모드 전환 입력은 임시로 마우스 우클릭 "홀드"(누르는 동안 카메라 모드,
//         떼면 1인칭 복귀, UserInput 구독).

using System;
using DG.Tweening;
using UnityEngine;

public enum CameraMode
{
    FirstPerson, // 1인칭 시점
    CameraView   // 카메라(뷰파인더) 모드
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

    [Header("Input (임시 매핑)")]
    [Tooltip("모드 전환에 사용할 마우스 버튼")]
    [SerializeField] private MouseButton switchButton = MouseButton.Right;

    /// <summary>현재 카메라 모드. (정적 DomainSingleton.Current 와 구분하기 위해 CurrentMode)</summary>
    public CameraMode CurrentMode { get; private set; } = CameraMode.FirstPerson;

    /// <summary>카메라(뷰파인더) 모드 여부.</summary>
    public bool IsCameraView => CurrentMode == CameraMode.CameraView;

    /// <summary>현재 활성(렌더 중) 카메라.</summary>
    public Camera ActiveCamera => IsCameraView ? cameraViewCamera : firstPersonCamera;

    /// <summary>모드가 바뀔 때 발행된다.</summary>
    public event Action<CameraMode> OnModeChanged;

    private Tween _tween;
    private bool _transitioning;  // 트윈 진행 중 여부
    private float _progress;      // 0 = off, 1 = on

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
        UserInput.Instance.AddMouseListener(switchButton, KeyPhase.Down, EnterCameraView);
        UserInput.Instance.AddMouseListener(switchButton, KeyPhase.Up, ExitCameraView);
    }

    private void OnDisable()
    {
        if (UserInput.Instance != null)
        {
            UserInput.Instance.RemoveMouseListener(switchButton, KeyPhase.Down, EnterCameraView);
            UserInput.Instance.RemoveMouseListener(switchButton, KeyPhase.Up, ExitCameraView);
        }
    }

    private void EnterCameraView() => SetMode(CameraMode.CameraView);
    private void ExitCameraView() => SetMode(CameraMode.FirstPerson);

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _tween?.Kill();
    }

    // main 카메라가 Player_Ctrl.Update 에서 회전/이동을 마친 뒤, 그 월드 트랜스폼 기준으로 자세를 배치한다.
    private void LateUpdate()
    {
        ApplyPose();
    }

    /// <summary>1인칭 ↔ 카메라 모드 토글.</summary>
    public void ToggleMode() => SetMode(IsCameraView ? CameraMode.FirstPerson : CameraMode.CameraView);

    /// <summary>지정한 모드로 전환하고 트윈 연출을 재생한다.</summary>
    public void SetMode(CameraMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        PlayTransition(mode);
        OnModeChanged?.Invoke(mode);
    }

    private void InitFirstPerson()
    {
        CurrentMode = CameraMode.FirstPerson;
        _transitioning = false;
        _progress = 0f;
        if (cameraViewCamera != null) cameraViewCamera.gameObject.SetActive(false);
        if (firstPersonCamera != null) firstPersonCamera.enabled = true;
        ApplyPose();
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
            .OnComplete(() =>
            {
                // 상승 완료 시점에 카메라 모드로 렌더 전환(들어올림은 fps 화면으로 보여줬으므로).
                if (mode == CameraMode.CameraView)
                {
                    if (cameraViewCamera != null) cameraViewCamera.gameObject.SetActive(true);
                    if (firstPersonCamera != null) firstPersonCamera.enabled = false;
                }
                _transitioning = false;
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
