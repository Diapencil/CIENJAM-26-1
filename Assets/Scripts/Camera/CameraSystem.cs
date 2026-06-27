// CameraSystem.cs
// 기능: 플레이어가 들고 있는 카메라의 조작과 배터리(칸제) 자원을 관리한다.
//       - 일반 플래시(탭) / 차지 플래시(홀드) 입력을 UserInput 구독으로 받아 처리.
//       - 플래시 시 배터리 1칸 차감 → 플래시 라이트 점등 → 활성 카메라로 화면을 RenderTexture 캡처
//         (스크린 오버레이 HUD 는 캡처에서 제외됨) → PhotoAlbum 에 저장 →
//         푸앙이(puangTag)가 화면 안(프러스텀)+거리 내+비차단이면 스턴.
//       - 배터리는 일반/차지(고성능) 두 풀로 분리. 일반은 칸제, 고성능은 용량 1(맵에 1개)이며
//         차지 플래시 1회로 전량 소모된다.
// 사용법: Player GameObject(Player_Ctrl 이 있는 오브젝트)에 본 컴포넌트를 추가하고 인스펙터에서
//         flashLight(점등용 Light)와 puangTag/occlusionMask, 소모/지속 값들을 설정한다. 촬영은 카메라 모드의
//         촬영 서브모드(CameraController.CurrentViewMode == Shoot)에서만 동작하며, 앨범 모드에서는 무반응이다.
//         HUD 등 외부는 OnNormalBatteryChanged / OnChargeBatteryChanged 를 구독해 표시를 갱신한다.
//         푸앙이 AI 등은 static OnFlashFired(위치, 거리) 를 구독해 플래시 소리를 청각 자극으로 받는다.
//
// 임시 입력 매핑:
//   좌클릭 탭            → 일반 플래시(일반 배터리 1칸)
//   좌클릭 홀드(2초) 후 떼기 → 차지 플래시(고성능 배터리 1칸 = 전량)
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
    [Tooltip("차지(고성능) 배터리 최대 칸 (용량 1, 맵에 1개)")]
    [SerializeField] private int chargeMax = 1;
    [Tooltip("게임 시작 시 일반 배터리 보유 칸 (최대 칸으로 클램프)")]
    [SerializeField] private int normalStart = 4;
    [Tooltip("게임 시작 시 차지(고성능) 배터리 보유 칸 (보통 0 — 맵에서 획득)")]
    [SerializeField] private int chargeStart = 0;
    [Tooltip("일반 플래시 1회 소모 칸(일반 배터리)")]
    [SerializeField] private int normalFlashCost = 1;
    [Tooltip("차지 플래시 1회 소모 칸(고성능 배터리). 용량 1을 1회로 전량 소모")]
    [SerializeField] private int chargeFlashCost = 1;

    [Header("플래시 스턴")]
    [Tooltip("일반 플래시 유효 거리(m)")]
    [SerializeField] private float normalRange = 5f;
    [Tooltip("차지 플래시 유효 거리(m)")]
    [SerializeField] private float chargeRange = 8f;
    [Tooltip("일반 플래시 스턴 지속(초)")]
    [SerializeField] private float normalStunDuration = 1f;
    [Tooltip("차지 플래시 스턴 지속(초)")]
    [SerializeField] private float chargeStunDuration = 3f;
    [Tooltip("스턴 대상(푸앙이) 태그. 화면 안 + 거리 내 + 비차단일 때 스턴")]
    [SerializeField] private string puangTag = "Puang";
    [Tooltip("시야 차단(장애물) 판정 레이어. 이 레이어 콜라이더에 가리면 스턴 실패")]
    [SerializeField] private LayerMask occlusionMask = ~0;

    [Header("차지")]
    [Tooltip("차지 플래시로 인정되는 최소 홀드 시간(초). 이보다 짧으면 일반 플래시")]
    [SerializeField] private float chargeHoldTime = 2f;

    [Header("셔터")]
    [Tooltip("촬영(플래시) 후 다음 촬영까지 쿨타임(초). 이 시간 동안 좌클릭 촬영 입력은 무시된다.")]
    [SerializeField] private float shutterCooldown = 0.5f;

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
    private float _nextShotTime;    // 셔터 쿨타임 종료 시각(Time.time). 이 시각 전엔 촬영 불가

    /// <summary>일반 배터리 변화 시 발행. (현재 칸, 최대 칸)</summary>
    public event Action<int, int> OnNormalBatteryChanged;
    /// <summary>차지 배터리 변화 시 발행. (현재 칸, 최대 칸)</summary>
    public event Action<int, int> OnChargeBatteryChanged;
    /// <summary>플래시 발사 시 발행. (발광 위치, 유효 거리) — 푸앙이 등 청각 AI가 소음원으로 사용.</summary>
    public static event Action<Vector3, float> OnFlashFired;

    public int NormalSlots => _normalSlots;
    public int ChargeSlots => _chargeSlots;
    public int NormalMax => normalMax;
    public int ChargeMax => chargeMax;

    private void Awake()
    {
        _normalSlots = Mathf.Clamp(normalStart, 0, normalMax);
        _chargeSlots = Mathf.Clamp(chargeStart, 0, chargeMax);
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
        if (!IsShootMode) return; // 카메라 모드 + 촬영 서브모드에서만 (앨범 모드에서는 무반응)
        _charging = true;
        _pressTime = Time.time;
    }

    private void OnLeftUp()
    {
        if (!_charging) return;
        _charging = false;

        if (!IsShootMode) return; // 누른 뒤 모드/서브모드가 바뀌면 취소

        bool isCharge = (Time.time - _pressTime) >= chargeHoldTime;
        if (isCharge) TryChargeFlash();
        else TryNormalFlash();
    }

    // 카메라(뷰파인더) 모드이면서 촬영 서브모드일 때만 플래시/촬영을 허용한다.
    private bool IsShootMode =>
        CameraController.Current != null
        && CameraController.Current.IsCameraView
        && CameraController.Current.CurrentViewMode == CameraViewMode.Shoot;

    // ── 플래시 ──────────────────────────────────────────────
    // 셔터 쿨타임 진행 중(또는 플래시 시퀀스 진행 중)이면 촬영 불가.
    private bool OnShutterCooldown => _isFlashing || Time.time < _nextShotTime;

    private void TryNormalFlash()
    {
        if (OnShutterCooldown) return;
        if (_normalSlots < normalFlashCost) return; // 배터리 부족 → 무반응
        SetNormalSlots(_normalSlots - normalFlashCost);
        _nextShotTime = Time.time + shutterCooldown;
        StartCoroutine(FlashRoutine(normalRange, normalStunDuration));
    }

    private void TryChargeFlash()
    {
        if (OnShutterCooldown) return;
        if (_chargeSlots < chargeFlashCost) return;
        SetChargeSlots(_chargeSlots - chargeFlashCost);
        _nextShotTime = Time.time + shutterCooldown;
        StartCoroutine(FlashRoutine(chargeRange, chargeStunDuration));
    }

    private IEnumerator FlashRoutine(float range, float stunDuration)
    {
        _isFlashing = true;

        if (flashLight != null) flashLight.enabled = true;
        yield return null; // 라이트 적용된 프레임으로 렌더되도록 1프레임 대기

        CapturePhoto();          // 플래시로 밝아진 장면을 사진으로 저장
        StunTargetInView(range, stunDuration); // 스턴 대상만 감지

        // 플래시 "소리"(자극) 브로드캐스트 — 청각 AI(푸앙이)가 소음원으로 받아 탐색한다.
        Vector3 flashPos = (CameraController.Current != null && CameraController.Current.ActiveCamera != null)
            ? CameraController.Current.ActiveCamera.transform.position
            : transform.position;
        OnFlashFired?.Invoke(flashPos, range);

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
    // 푸앙이(단일, puangTag)가 활성 카메라의 화면 안(프러스텀)에, 유효 거리 이내에 있고,
    // 장애물에 가려지지 않았을 때만 스턴한다. (화면 중앙이 아니어도 "화면에 잡히면" 인정)
    private void StunTargetInView(float range, float stunDuration)
    {
        var cam = CameraController.Current != null ? CameraController.Current.ActiveCamera : null;
        if (cam == null) return;

        GameObject puang = GameObject.FindWithTag(puangTag);
        if (puang == null) return;

        IStunTarget target = puang.GetComponentInChildren<IStunTarget>();
        if (target == null) return;

        // 판정 기준점: 콜라이더 중심(없으면 transform 위치)
        Vector3 camPos = cam.transform.position;
        Bounds targetBounds = GetTargetBounds(puang);
        Vector3 targetPos = targetBounds.center;

        // 1) 거리
        if (Vector3.Distance(camPos, targetPos) > range) return;

        // 2) 화면 안(프러스텀): 뷰포트 0~1 범위 + 카메라 앞(z>0)
        if (!TryGetVisibleTargetPoint(cam, puang.transform, targetBounds, out targetPos)) return;

        // 3) 장애물 차단: 카메라→대상 사이에 푸앙이가 아닌 콜라이더가 막으면 실패
        target.OnFlashStunned(stunDuration);
    }

    // ── 배터리 갱신 ─────────────────────────────────────────
    private Bounds GetTargetBounds(GameObject target)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            if (c == null || !c.enabled) continue;
            if (!hasBounds)
            {
                bounds = c.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        return bounds;
    }

    private bool TryGetVisibleTargetPoint(Camera cam, Transform targetRoot, Bounds bounds, out Vector3 visiblePoint)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector3[] points =
        {
            center,
            center + new Vector3( extents.x,  extents.y,  extents.z),
            center + new Vector3( extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y,  extents.z),
            center + new Vector3( extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x, -extents.y, -extents.z),
        };

        foreach (Vector3 point in points)
        {
            if (!IsInViewport(cam, point)) continue;
            if (!HasLineOfSight(cam.transform.position, point, targetRoot)) continue;
            visiblePoint = point;
            return true;
        }

        visiblePoint = center;
        return false;
    }

    private static bool IsInViewport(Camera cam, Vector3 worldPoint)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPoint);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to, Transform targetRoot)
    {
        if (!Physics.Linecast(from, to, out RaycastHit hit, occlusionMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform hitTransform = hit.collider.transform;
        return hitTransform == targetRoot || hitTransform.IsChildOf(targetRoot);
    }

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
