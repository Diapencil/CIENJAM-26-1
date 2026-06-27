// CameraSystem.cs
// 기능: 플레이어가 들고 있는 카메라의 조작과 배터리(칸제) 자원을 관리한다.
//       - 일반 플래시(탭) / 차지 플래시(홀드) 입력을 UserInput 구독으로 받아 처리.
//       - 플래시 시 배터리 1칸 차감 → 플래시 라이트 점등 → 활성 카메라로 화면을 RenderTexture 캡처
//         (스크린 오버레이 HUD 는 캡처에서 제외됨) → PhotoAlbum 에 저장 → Raycast 로 스턴 대상만 감지.
//       - 배터리는 일반/차지(고성능) 두 풀로 분리, 각각 최대 4칸.
// 사용법: Player GameObject(Player_Ctrl 이 있는 오브젝트)에 본 컴포넌트를 추가하고 인스펙터에서
//         flashLight(점등용 Light)와 stunMask, 소모/지속 값들을 설정한다. 촬영은 카메라 모드
//         (CameraController.IsCameraView == true)에서만 동작한다.
//         HUD 등 외부는 OnNormalBatteryChanged / OnChargeBatteryChanged 를 구독해 표시를 갱신한다.
//
// 임시 입력 매핑:
//   좌클릭 탭            → 일반 플래시(일반 배터리 1칸)
//   좌클릭 홀드(2초) 후 떼기 → 차지 플래시(고성능 배터리 2칸)
//   모드 전환(우클릭)은 CameraController 가 담당.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraSystem : MonoBehaviour
{
    [Header("배터리 (칸제)")]
    [Tooltip("일반 배터리 최대 칸")]
    [SerializeField] private int normalMax = 4;
    [Tooltip("차지(고성능) 배터리 최대 칸")]
    [SerializeField] private int chargeMax = 4;
    [Tooltip("일반 플래시 1회 소모 칸(일반 배터리)")]
    [SerializeField] private int normalFlashCost = 1;
    [Tooltip("차지 플래시 1회 소모 칸(고성능 배터리)")]
    [SerializeField] private int chargeFlashCost = 2;

    [Header("플래시 스턴")]
    [Tooltip("일반 플래시 유효 거리(m)")]
    [SerializeField] private float normalRange = 5f;
    [Tooltip("차지 플래시 유효 거리(m)")]
    [SerializeField] private float chargeRange = 8f;
    [Tooltip("일반 플래시 스턴 지속(초)")]
    [SerializeField] private float normalStunDuration = 1f;
    [Tooltip("차지 플래시 스턴 지속(초)")]
    [SerializeField] private float chargeStunDuration = 3f;
    [Tooltip("스턴 대상 감지 레이어")]
    [SerializeField] private LayerMask stunMask = ~0;

    [Header("차지")]
    [Tooltip("차지 플래시로 인정되는 최소 홀드 시간(초). 이보다 짧으면 일반 플래시")]
    [SerializeField] private float chargeHoldTime = 2f;

    [Header("연출")]
    [Tooltip("촬영 순간 점등할 플래시 Light")]
    [SerializeField] private Light flashLight;
    [Tooltip("플래시 라이트 점등 유지 시간(초)")]
    [SerializeField] private float flashLightDuration = 0.08f;

    // ── 상태 ───────────────────────────────────────────────
    private int _normalSlots;
    private int _chargeSlots;
    private bool _isFlashing;       // 플래시 시퀀스 진행 중(중복 방지)
    private bool _charging;         // 좌클릭 홀드 중
    private float _pressTime;       // 좌클릭 누른 시각

    /// <summary>일반 배터리 변화 시 발행. (현재 칸, 최대 칸)</summary>
    public event Action<int, int> OnNormalBatteryChanged;
    /// <summary>차지 배터리 변화 시 발행. (현재 칸, 최대 칸)</summary>
    public event Action<int, int> OnChargeBatteryChanged;

    public int NormalSlots => _normalSlots;
    public int ChargeSlots => _chargeSlots;
    public int NormalMax => normalMax;
    public int ChargeMax => chargeMax;

    private void Awake()
    {
        _normalSlots = normalMax;
        _chargeSlots = chargeMax;
    }

    private void Start()
    {
        // 초기값을 구독자(HUD)에게 1회 통지
        OnNormalBatteryChanged?.Invoke(_normalSlots, normalMax);
        OnChargeBatteryChanged?.Invoke(_chargeSlots, chargeMax);
    }

    private void OnEnable()
    {
        UserInput.Instance.AddMouseListener(MouseButton.Left, KeyPhase.Down, OnLeftDown);
        UserInput.Instance.AddMouseListener(MouseButton.Left, KeyPhase.Up, OnLeftUp);
    }

    private void OnDisable()
    {
        if (UserInput.Instance != null)
        {
            UserInput.Instance.RemoveMouseListener(MouseButton.Left, KeyPhase.Down, OnLeftDown);
            UserInput.Instance.RemoveMouseListener(MouseButton.Left, KeyPhase.Up, OnLeftUp);
        }
    }

    // ── 입력 처리 ───────────────────────────────────────────
    private void OnLeftDown()
    {
        if (!IsCameraView) return; // 카메라 모드에서만
        _charging = true;
        _pressTime = Time.time;
    }

    private void OnLeftUp()
    {
        if (!_charging) return;
        _charging = false;

        if (!IsCameraView) return; // 누른 뒤 모드가 바뀌면 취소

        bool isCharge = (Time.time - _pressTime) >= chargeHoldTime;
        if (isCharge) TryChargeFlash();
        else TryNormalFlash();
    }

    private bool IsCameraView => CameraController.Current != null && CameraController.Current.IsCameraView;

    // ── 플래시 ──────────────────────────────────────────────
    private void TryNormalFlash()
    {
        if (_isFlashing) return;
        if (_normalSlots < normalFlashCost) return; // 배터리 부족 → 무반응
        SetNormalSlots(_normalSlots - normalFlashCost);
        StartCoroutine(FlashRoutine(normalRange, normalStunDuration));
    }

    private void TryChargeFlash()
    {
        if (_isFlashing) return;
        if (_chargeSlots < chargeFlashCost) return;
        SetChargeSlots(_chargeSlots - chargeFlashCost);
        StartCoroutine(FlashRoutine(chargeRange, chargeStunDuration));
    }

    private IEnumerator FlashRoutine(float range, float stunDuration)
    {
        _isFlashing = true;

        if (flashLight != null) flashLight.enabled = true;
        yield return null; // 라이트 적용된 프레임으로 렌더되도록 1프레임 대기

        CapturePhoto();          // 플래시로 밝아진 장면을 사진으로 저장
        StunTargetInView(range, stunDuration); // 스턴 대상만 감지

        yield return new WaitForSeconds(flashLightDuration);
        if (flashLight != null) flashLight.enabled = false;

        _isFlashing = false;
    }

    // ── 촬영(캡처) ──────────────────────────────────────────
    // 활성 카메라를 임시 RenderTexture 로 렌더(URP SubmitRenderRequest)해 Texture2D 로 읽는다.
    // 스크린 오버레이로 그려지는 UI Toolkit HUD 는 카메라 렌더에 포함되지 않으므로 사진에서 제외된다.
    private void CapturePhoto()
    {
        var cam = CameraController.Current != null ? CameraController.Current.ActiveCamera : null;
        if (cam == null) return;

        int w = Screen.width;
        int h = Screen.height;
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24);

        // URP: 카메라를 지정 RenderTexture 로 온디맨드 렌더 (Camera.Render() 는 SRP 에서 동작하지 않음)
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request))
        {
            RenderPipeline.SubmitRenderRequest(cam, request);
        }
        else
        {
            RenderTexture.ReleaseTemporary(rt);
            Debug.LogWarning("[CameraSystem] SingleCameraRequest 미지원 — 캡처 생략.", this);
            return;
        }

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        RenderTexture.ReleaseTemporary(rt);

        PhotoAlbum.Current?.Add(tex);
    }

    // ── 스턴 판정 ───────────────────────────────────────────
    private void StunTargetInView(float range, float stunDuration)
    {
        var cam = CameraController.Current != null ? CameraController.Current.ActiveCamera : null;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range, stunMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.TryGetComponent<IStunTarget>(out var target))
                target.OnFlashStunned(stunDuration);
        }
    }

    // ── 배터리 갱신 ─────────────────────────────────────────
    private void SetNormalSlots(int value)
    {
        _normalSlots = Mathf.Clamp(value, 0, normalMax);
        OnNormalBatteryChanged?.Invoke(_normalSlots, normalMax);
    }

    private void SetChargeSlots(int value)
    {
        _chargeSlots = Mathf.Clamp(value, 0, chargeMax);
        OnChargeBatteryChanged?.Invoke(_chargeSlots, chargeMax);
    }

    /// <summary>아이템 획득 등으로 일반 배터리를 충전한다(칸 단위).</summary>
    public void AddNormalSlots(int slots) => SetNormalSlots(_normalSlots + slots);

    /// <summary>아이템 획득 등으로 차지(고성능) 배터리를 충전한다(칸 단위).</summary>
    public void AddChargeSlots(int slots) => SetChargeSlots(_chargeSlots + slots);
}
