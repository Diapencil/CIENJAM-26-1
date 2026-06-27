// PuangAI.cs
// 기능: 푸앙이의 적 AI. NavMeshAgent 기반으로 움직이며 5개 상태(Idle / Wander / Curious / Chase / Stun)
//       FSM 으로 동작한다. 플레이어를 시야(전방 시야각·거리·장애물 차단)와 청각(발소리·플래시 소리)으로
//       감지해 추적하며, 카메라 플래시에 피격되면 IStunTarget.OnFlashStunned 로 스턴된다.
//       - 발소리: 플레이어 Transform 의 프레임당 이동량으로 속도를 측정해 정지/걷기/달리기를 분류하고,
//                 각 상태별 감지 반경(walk/run) 안에 있으면 소음원으로 인식해 Curious 로 전환한다.
//       - 플래시 소리: CameraSystem.OnFlashFired(static) 를 구독해 플래시 위치를 소음원으로 받는다.
//       - 잡힘: Chase 중 catchDistance 이내로 접근하면 OnCaughtPlayer 이벤트를 1회 발행한다(연출/게임오버는 외부가 연결).
// 사용법: 푸앙이 GameObject(태그 "Puang", NavMeshAgent 포함, NavMesh 위)에 본 컴포넌트를 추가한다.
//         인스펙터에서 wanderRadius(배회 반경), 속도/감지 파라미터, occlusionMask 를 설정한다. player 는
//         미할당 시 태그 "Player" 로 자동 탐색한다. 플래시 스턴은 CameraSystem 이 IStunTarget 으로 자동 호출한다.
//         외부는 OnCaughtPlayer / OnStateChanged 를 구독해 게임오버·애니메이션·사운드를 연결한다.

using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PuangAI : MonoBehaviour, IStunTarget
{
    public enum PuangState { Idle, Wander, Curious, Chase, Stun }

    [Header("참조")]
    [Tooltip("추적 대상 플레이어. 미할당 시 태그 \"Player\" 로 자동 탐색")]
    [SerializeField] private Transform player;

    [Header("Wander (랜덤 배회)")]
    [Tooltip("현재 위치 기준 이 반경 안의 NavMesh 위 랜덤 지점으로 이동(m)")]
    [SerializeField] private float wanderRadius = 10f;
    [Tooltip("랜덤 지점 샘플링 최대 시도 횟수")]
    [SerializeField] private int wanderSampleTries = 8;

    [Header("이동 속도 (m/s)")]
    [SerializeField] private float wanderSpeed = 0.8f;
    [SerializeField] private float curiousSpeed = 1.2f;
    [SerializeField] private float chaseSpeed = 2.5f;
    [Tooltip("가속도 (코너에서 잠깐 느려지는 효과)")]
    [SerializeField] private float acceleration = 2.0f;
    [Tooltip("회전 속도 (°/s)")]
    [SerializeField] private float angularSpeed = 180f;

    [Header("Idle (웨이포인트 대기)")]
    [SerializeField] private float idleWaitMin = 1f;
    [SerializeField] private float idleWaitMax = 3f;

    [Header("시야 감지")]
    [Tooltip("전방 시야각(도). 이 각도 안 + 거리 안 + 비차단이면 발견")]
    [SerializeField] private float visionAngle = 120f;
    [Tooltip("시야 감지 거리(m)")]
    [SerializeField] private float visionRange = 6f;
    [Tooltip("시야 차단(장애물) 판정 레이어. 이 레이어에 가리면 미발견")]
    [SerializeField] private LayerMask occlusionMask = ~0;
    [Tooltip("눈 높이 기준점(미할당 시 transform). 시야 raycast 시작점")]
    [SerializeField] private Transform eye;

    [Header("청각 감지 - 발소리")]
    [Tooltip("이 속도 미만이면 정지로 간주(소음 없음)")]
    [SerializeField] private float moveEpsilon = 0.2f;
    [Tooltip("이 속도 이상이면 달리기로 간주(걷기/달리기 경계, m/s)")]
    [SerializeField] private float runSpeedThreshold = 7f;
    [Tooltip("걷기 발소리 감지 반경(m)")]
    [SerializeField] private float walkHearRadius = 3f;
    [Tooltip("달리기 발소리 감지 반경(m)")]
    [SerializeField] private float runHearRadius = 7f;

    [Header("청각 감지 - 플래시 소리")]
    [Tooltip("플래시 소리 감지 반경(m). 이 안에서 플래시가 터지면 Curious 로 전환")]
    [SerializeField] private float flashHearRadius = 10f;

    [Header("Curious (소음 탐색)")]
    [Tooltip("소음 좌표 도착 후 주변 랜덤 탐색 반경(m)")]
    [SerializeField] private float curiousSearchRadius = 2f;
    [Tooltip("탐색 지속 시간(초). 종료 시 플레이어 미발견이면 Idle 복귀")]
    [SerializeField] private float curiousSearchTime = 3f;

    [Header("Chase (추적)")]
    [Tooltip("이 거리 이내로 접근하면 잡힘 판정(OnCaughtPlayer)")]
    [SerializeField] private float catchDistance = 1.5f;
    [Tooltip("시야에서 사라진 뒤 추적을 포기하기까지의 시간(초)")]
    [SerializeField] private float loseSightTime = 8f;

    [Header("Stun")]
    [Tooltip("스턴 종료 후 재피격 무시 시간(연속 플래시 방지, 초)")]
    [SerializeField] private float stunInvincibleTime = 0.5f;

    [Header("도착 판정")]
    [Tooltip("목적지 도착으로 간주하는 잔여 거리(m)")]
    [SerializeField] private float arriveThreshold = 0.3f;

    [Header("디버그")]
    [Tooltip("선택 시 시야/청각/경로 기즈모 표시")]
    [SerializeField] private bool drawGizmos = true;
    [Tooltip("상태 전이를 콘솔에 로그로 출력")]
    [SerializeField] private bool logStateChange = true;

    /// <summary>Chase 중 플레이어를 catchDistance 이내로 잡은 순간 1회 발행.</summary>
    public event Action OnCaughtPlayer;
    /// <summary>상태가 바뀔 때 발행(애니메이션·사운드 연결용).</summary>
    public event Action<PuangState> OnStateChanged;

    public PuangState State => _state;

    private NavMeshAgent _agent;
    private Rigidbody _rigidbody;
    private PuangState _state = PuangState.Idle;
    private NavMeshPath _wanderPath;   // Wander 도달성 검사용 경로 버퍼(매번 new 방지, GC 절감)

    // 타이머/상태값
    private float _stateTimer;        // 상태별 범용 타이머(Idle 대기, Curious 탐색, Stun 잔여)
    private Vector3 _lastHeardPos;     // 마지막 소음 좌표(Curious 목표)
    private Vector3 _lastSeenPos;      // 마지막 목격 좌표(Chase 추적 실패 시 이동 목표)
    private float _loseSightTimer;     // 시야 상실 누적 시간
    private float _invincibleUntil;    // 이 시각 전까지 재스턴 무시
    private bool _hasCaught;           // 잡힘 1회 발행 가드

    // 플레이어 속도 측정
    private Vector3 _prevPlayerPos;
    private float _playerSpeed;
    private bool _curiousSearching;    // Curious: 소음 좌표 도착 후 주변 탐색 단계 진입 여부

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rigidbody = GetComponent<Rigidbody>();
        _wanderPath = new NavMeshPath();
        _agent.acceleration = acceleration;
        _agent.angularSpeed = angularSpeed;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (eye == null) eye = transform;
    }

    private void OnEnable()
    {
        CameraSystem.OnFlashFired += OnFlashHeard;
    }

    private void OnDisable()
    {
        CameraSystem.OnFlashFired -= OnFlashHeard;
    }

    private void Start()
    {
        if (player != null) _prevPlayerPos = player.position;
        EnterIdle();
    }

    private void Update()
    {
        MeasurePlayerSpeed();

        switch (_state)
        {
            case PuangState.Idle:    TickIdle();    break;
            case PuangState.Wander:  TickWander();  break;
            case PuangState.Curious: TickCurious(); break;
            case PuangState.Chase:   TickChase();   break;
            case PuangState.Stun:    TickStun();    break;
        }
    }

    // ── 감각(공통) ──────────────────────────────────────────
    private void MeasurePlayerSpeed()
    {
        if (player == null || Time.deltaTime <= 0f) return;
        _playerSpeed = Vector3.Distance(player.position, _prevPlayerPos) / Time.deltaTime;
        _prevPlayerPos = player.position;
    }

    // 발소리: 플레이어 이동 상태별 감지 반경 안이면 소음원 인식.
    private bool HearsFootstep()
    {
        if (player == null) return false;
        float radius;
        if (_playerSpeed >= runSpeedThreshold) radius = runHearRadius;
        else if (_playerSpeed >= moveEpsilon) radius = walkHearRadius;
        else return false; // 정지 → 무음
        return Vector3.Distance(transform.position, player.position) <= radius;
    }

    // 시야: 시야각 + 거리 + 장애물 비차단.
    private bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector3 from = eye.position;
        Vector3 to = player.position;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist > visionRange) return false;
        if (Vector3.Angle(transform.forward, dir) > visionAngle * 0.5f) return false;
        // 장애물 차단: 사이에 occlusionMask 콜라이더가 막으면 미발견(플레이어 자신 콜라이더는 무시)
        if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, dist, occlusionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != player && !hit.transform.IsChildOf(player)) return false;
        }
        return true;
    }

    // 플래시 소리(static 이벤트): 감지 반경 안이면 그 위치를 소음원으로 Curious.
    private void OnFlashHeard(Vector3 pos, float range)
    {
        if (_state == PuangState.Stun) return;
        if (Vector3.Distance(transform.position, pos) > flashHearRadius) return;
        _lastHeardPos = pos;
        if (_state != PuangState.Chase) EnterCurious();
    }

    // ── Idle ────────────────────────────────────────────────
    private void EnterIdle()
    {
        SetState(PuangState.Idle);
        StopAgent();
        _stateTimer = UnityEngine.Random.Range(idleWaitMin, idleWaitMax);
    }

    private void TickIdle()
    {
        if (DetectByEar() || DetectByEye()) return;
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) EnterWander();
    }

    // ── Wander ──────────────────────────────────────────────
    private void EnterWander()
    {
        SetState(PuangState.Wander);
        if (!TryGetRandomWanderPoint(out Vector3 dest)) { EnterIdle(); return; }
        ResumeAgent(wanderSpeed);
        _agent.SetDestination(dest);
    }

    private void TickWander()
    {
        if (DetectByEar() || DetectByEye()) return;
        if (Arrived()) EnterIdle();
    }

    // 현재 위치 기준 wanderRadius 안에서 NavMesh 위 이동 가능한 랜덤 지점을 찾는다.
    // SamplePosition 으로 NavMesh 위 점을 찾은 뒤, CalculatePath 로 현재 위치에서
    // 완전히 도달 가능한(PathComplete) 점만 채택한다(분리된 섬·막힌 구역 배제).
    private bool TryGetRandomWanderPoint(out Vector3 result)
    {
        for (int i = 0; i < wanderSampleTries; i++)
        {
            Vector3 candidate = transform.position + UnityEngine.Random.insideUnitSphere * wanderRadius;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                continue;
            // 현재 위치에서 후보까지 완전 경로가 있는지 확인(부분 경로/연결 안 됨 배제)
            if (!_agent.CalculatePath(hit.position, _wanderPath)) continue;
            if (_wanderPath.status != NavMeshPathStatus.PathComplete) continue;
            result = hit.position;
            return true;
        }
        result = transform.position;
        return false;
    }

    // ── Curious ─────────────────────────────────────────────
    private void EnterCurious()
    {
        SetState(PuangState.Curious);
        ResumeAgent(curiousSpeed);
        _agent.SetDestination(_lastHeardPos);
        _curiousSearching = false;
        _stateTimer = curiousSearchTime;
    }

    private void TickCurious()
    {
        if (DetectByEye()) return; // 탐색 중 시야 진입 시 즉시 Chase

        if (!_curiousSearching)
        {
            if (Arrived()) _curiousSearching = true; // 소음 좌표 도착 → 주변 탐색 단계
            return;
        }

        _stateTimer -= Time.deltaTime;
        if (Arrived()) // 탐색 지점 도달 시 새 랜덤 지점
        {
            Vector3 rnd = _lastHeardPos + UnityEngine.Random.insideUnitSphere * curiousSearchRadius;
            rnd.y = _lastHeardPos.y;
            if (NavMesh.SamplePosition(rnd, out NavMeshHit nh, curiousSearchRadius, NavMesh.AllAreas))
                _agent.SetDestination(nh.position);
        }
        if (_stateTimer <= 0f) EnterIdle(); // 시간 종료 → 미발견 → Idle
    }

    // ── Chase ───────────────────────────────────────────────
    private void EnterChase()
    {
        SetState(PuangState.Chase);
        ResumeAgent(chaseSpeed);
        _loseSightTimer = 0f;
        _hasCaught = false;
    }

    private void TickChase()
    {
        if (player == null) { EnterIdle(); return; }

        bool sees = CanSeePlayer();
        if (sees)
        {
            _lastSeenPos = player.position;
            _loseSightTimer = 0f;
            _agent.SetDestination(player.position);

            if (!_hasCaught && Vector3.Distance(transform.position, player.position) <= catchDistance)
            {
                _hasCaught = true;
                OnCaughtPlayer?.Invoke();
            }
        }
        else
        {
            // 시야 상실 → 마지막 목격 위치로 이동, 일정 시간 후 포기
            _agent.SetDestination(_lastSeenPos);
            _loseSightTimer += Time.deltaTime;
            if (_loseSightTimer >= loseSightTime) EnterIdle();
        }
    }

    // ── Stun (IStunTarget) ──────────────────────────────────
    public void OnFlashStunned(float duration)
    {
        if (Time.time < _invincibleUntil) return; // 무적 프레임 중 재피격 무시
        SetState(PuangState.Stun);
        StopAgent();
        StopPhysics();
        _stateTimer = duration;
    }

    private void TickStun()
    {
        StopPhysics();
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            _invincibleUntil = Time.time + stunInvincibleTime;
            EnterChase(); // 회복 후 추적 재개
        }
    }

    // ── 감지 보조(전이 트리거) ──────────────────────────────
    // 비경계 상태(Idle/Wander/Curious)에서 발소리 감지 시 Curious 전환.
    private bool DetectByEar()
    {
        if (!HearsFootstep()) return false;
        _lastHeardPos = player.position;
        EnterCurious();
        return true;
    }

    // 비경계 상태에서 시야 발견 시 Chase 전환.
    private bool DetectByEye()
    {
        if (!CanSeePlayer()) return false;
        _lastSeenPos = player.position;
        EnterChase();
        return true;
    }

    // ── NavMeshAgent 제어 ───────────────────────────────────
    private void StopAgent()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();
    }

    private void StopPhysics()
    {
        if (_rigidbody == null) return;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void ResumeAgent(float speed)
    {
        _agent.speed = speed;
        if (_agent.isOnNavMesh) _agent.isStopped = false;
    }

    private bool Arrived()
    {
        if (_agent.pathPending) return false;
        return _agent.remainingDistance <= Mathf.Max(arriveThreshold, _agent.stoppingDistance);
    }

    private void SetState(PuangState s)
    {
        if (_state == s) return;
        if (logStateChange) Debug.Log($"[PuangAI] {_state} → {s}", this);
        _state = s;
        OnStateChanged?.Invoke(s);
    }

#if UNITY_EDITOR
    // ── 에디터 기즈모 ───────────────────────────────────────
    // 선택 시 시야 콘(각도+거리), 청각 반경(발소리 걷기/달리기·플래시), 잡힘 거리,
    // 웨이포인트 경로, 런타임 목표/마지막 소음·목격 위치를 시각화한다.
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 center = transform.position;
        Vector3 eyePos = eye != null ? eye.position : center;

        // 시야 콘 (노랑) — 수평 평면 기준 visionAngle / visionRange
        Vector3 left = Quaternion.AngleAxis(-visionAngle * 0.5f, Vector3.up) * transform.forward;
        UnityEditor.Handles.color = new Color(1f, 0.92f, 0.2f, 0.08f);
        UnityEditor.Handles.DrawSolidArc(eyePos, Vector3.up, left, visionAngle, visionRange);
        UnityEditor.Handles.color = new Color(1f, 0.92f, 0.2f, 0.9f);
        UnityEditor.Handles.DrawWireArc(eyePos, Vector3.up, left, visionAngle, visionRange);
        Vector3 right = Quaternion.AngleAxis(visionAngle * 0.5f, Vector3.up) * transform.forward;
        UnityEditor.Handles.DrawLine(eyePos, eyePos + left * visionRange);
        UnityEditor.Handles.DrawLine(eyePos, eyePos + right * visionRange);
        UnityEditor.Handles.Label(eyePos + transform.forward * visionRange, $"Vision {visionAngle:0}° / {visionRange:0}m");

        // 청각 반경 — 발소리 걷기(초록)/달리기(주황), 플래시(하늘)
        DrawHearRing(center, walkHearRadius, new Color(0.2f, 1f, 0.3f, 0.9f), "Walk " + walkHearRadius + "m");
        DrawHearRing(center, runHearRadius, new Color(1f, 0.55f, 0.1f, 0.9f), "Run " + runHearRadius + "m");
        DrawHearRing(center, flashHearRadius, new Color(0.3f, 0.8f, 1f, 0.9f), "Flash " + flashHearRadius + "m");

        // 잡힘 거리 (빨강)
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, catchDistance);
        UnityEditor.Handles.Label(center + transform.forward * catchDistance, "Catch " + catchDistance + "m");

        // 배회 반경 (회색) — 이 안의 NavMesh 랜덤 지점으로 이동
        UnityEditor.Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, wanderRadius);
        UnityEditor.Handles.Label(center + Vector3.forward * wanderRadius, "Wander " + wanderRadius + "m");

        // 런타임 상태 — 현재 목표/마지막 소음·목격 위치
        if (Application.isPlaying)
        {
            if (_agent != null && _agent.hasPath)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(center, _agent.destination);
            }
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 1f); // 마지막 소음(하늘)
            Gizmos.DrawWireCube(_lastHeardPos, Vector3.one * 0.4f);
            Gizmos.color = Color.red;                      // 마지막 목격(빨강)
            Gizmos.DrawWireCube(_lastSeenPos, Vector3.one * 0.4f);
            UnityEditor.Handles.Label(center + Vector3.up * 2f, _state.ToString());
        }
    }

    private void DrawHearRing(Vector3 center, float radius, Color color, string label)
    {
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
        UnityEditor.Handles.Label(center + Vector3.right * radius, label);
    }
#endif
}
