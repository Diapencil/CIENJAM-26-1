// GameManager.cs
// 기능: 인게임(Play 씬)의 전체 흐름을 총괄하는 도메인 스코프 매니저.
//       - 게임 상태(GamePhase) 상태 기계: Intro → Playing → Escaping → Ending → Cleared.
//       - 기획 "세 기믹의 연결 구조"에 대응하는 탈출 진행 플래그(자물쇠/열쇠/2구역문/최종유도)를 관리.
//       - 상태·플래그 변경을 이벤트로 발행해 UI/사운드/문 오브젝트 등이 구독하게 한다.
//       개별 시스템(CameraSystem, 푸앙이 AI, 퍼즐 판정)의 내부 로직은 포함하지 않으며,
//       이 매니저는 상위 흐름과 승리 조건, 엔딩 트리거만 담당한다.
// 사용법: Play 씬의 빈 GameObject 에 본 컴포넌트를 붙인다(인게임 도메인 1개). 외부는
//           GameManager.Current.CurrentPhase / OnPhaseChanged          // 상태 조회·구독
//           GameManager.Current.UnlockGate1() / ObtainKey() / ...      // 퍼즐 측에서 진행 보고
//           GameManager.Current.TriggerFinalEscape()                   // 최종 유도 성공 시 호출
//           GameManager.Current.RestartGame() / ReturnToTitle()        // 흐름 제어
//         로 접근한다. 퍼즐·푸앙이 측은 GameManager 를 수정 없이 GameManager.Current 로 호출한다.

using System;
using UnityEngine;

/// <summary>인게임 전체 흐름 단계.</summary>
public enum GamePhase
{
    Intro,    // 진입 연출/대기 (아직 조작 불가 가능)
    Playing,  // 탐색·증거수집·회피 (코어 루프)
    Escaping, // 최종 탈출 시퀀스(압력센서 유도) 진행 중
    Ending,   // 엔딩 연출 재생 중
    Cleared,  // 탈출 완료(종료 상태)
    Dead      // 사망 연출/재시작 선택 중
}

public class GameManager : DomainSingleton<GameManager>
{
    public readonly struct DeathContext
    {
        public readonly string Reason;
        public readonly UnityEngine.Object Source;

        public DeathContext(string reason, UnityEngine.Object source)
        {
            Reason = reason;
            Source = source;
        }
    }

    [Header("씬 이름 (흐름 제어)")]
    [Tooltip("재시작 시 다시 로드할 인게임 씬 이름")]
    [SerializeField] private string playSceneName = "Play";
    [Tooltip("타이틀로 돌아갈 때 로드할 씬 이름")]
    [SerializeField] private string titleSceneName = "Start";

    [Header("초기 설정")]
    [Tooltip("Start 진입 시 자동으로 Playing 으로 전환할지 여부")]
    [SerializeField] private bool autoStartOnLoad = true;

    // ── 상태 ───────────────────────────────────────────────
    /// <summary>현재 게임 단계. (DomainSingleton.Current 와 구분해 CurrentPhase)</summary>
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Intro;

    /// <summary>Playing 진입 후 경과 시간(초). 표시·연출용(시간초과 패널티 없음).</summary>
    public float ElapsedTime { get; private set; }

    // ── 탈출 진행 플래그 (세 기믹 연동) ───────────────────────
    /// <summary>탈출기믹1: 연구기록 비밀번호로 1구역 자물쇠 해제.</summary>
    public bool Gate1Unlocked { get; private set; }
    public bool KeypadObtained { get; private set; }
    /// <summary>탈출기믹2-a: 비밀번호로 보관함 개방 → 열쇠 획득.</summary>
    public bool KeyObtained { get; private set; }
    /// <summary>탈출기믹2-b: 열쇠로 2구역 탈출문 개방.</summary>
    public bool Gate2Unlocked { get; private set; }
    /// <summary>탈출기믹3: 압력센서 위 차지 플래시로 STUN_FINAL 발동.</summary>
    public bool FinalEscapeTriggered { get; private set; }

    // ── 이벤트 ───────────────────────────────────────────────
    /// <summary>게임 단계가 바뀔 때 발행. 인자는 새 단계.</summary>
    public event Action<GamePhase> OnPhaseChanged;
    /// <summary>탈출 진행 플래그 중 하나가 켜질 때 발행. 인자는 켜진 플래그.</summary>
    public event Action<EscapeFlag> OnEscapeProgress;
    public event Action<DeathContext> OnPlayerDied;

    /// <summary>탈출 진행 단계 식별자.</summary>
    public enum EscapeFlag { Gate1, Keypad, Key, Gate2, FinalEscape }

    private bool _timing; // ElapsedTime 누적 여부(Playing/Escaping 동안만)

    private void Start()
    {
        if (autoStartOnLoad) StartGame();
    }

    private void Update()
    {
        if (_timing) ElapsedTime += Time.deltaTime;
    }

    // ── 흐름 제어 API ────────────────────────────────────────
    /// <summary>Intro 에서 코어 루프(Playing)로 진입한다.</summary>
    public void StartGame()
    {
        if (CurrentPhase != GamePhase.Intro) return;
        ElapsedTime = 0f;
        _timing = true;
        SetPhase(GamePhase.Playing);
    }

    /// <summary>최종 유도 성공(STUN_FINAL) 시 호출. 탈출 시퀀스 단계로 전환한다.</summary>
    public void TriggerFinalEscape()
    {
        if (CurrentPhase != GamePhase.Playing) return;
        FinalEscapeTriggered = true;
        OnEscapeProgress?.Invoke(EscapeFlag.FinalEscape);
        SetPhase(GamePhase.Escaping);
    }

    /// <summary>플레이어가 마지막 문을 통과해 탈출 성공. 엔딩 연출 단계로 전환한다.</summary>
    public void TriggerEnding()
    {
        if (CurrentPhase != GamePhase.Escaping) return;
        _timing = false;
        SetPhase(GamePhase.Ending);
    }

    /// <summary>엔딩 연출 종료 후 호출. 클리어 상태로 확정한다.</summary>
    public void CompleteClear()
    {
        if (CurrentPhase != GamePhase.Ending) return;
        _timing = false;
        SetPhase(GamePhase.Cleared);
    }

    public void KillPlayer(string reason = null, UnityEngine.Object source = null)
    {
        if (CurrentPhase == GamePhase.Dead)
        {
            Debug.Log($"[GameManager] KillPlayer ignored because phase is already Dead. reason='{reason}' source='{source}'", this);
            return;
        }

        _timing = false;
        var context = new DeathContext(reason, source);
        Debug.Log($"[GameManager] Player death requested. reason='{reason}' source='{source}' previousPhase={CurrentPhase}", this);
        SetPhase(GamePhase.Dead);
        OnPlayerDied?.Invoke(context);
    }

    /// <summary>인게임 씬을 재로드해 처음부터 다시 시작한다.</summary>
    public void RestartGame()
    {
        SceneController.Instance.LoadScene(playSceneName);
    }

    /// <summary>타이틀(Start) 씬으로 돌아간다.</summary>
    public void ReturnToTitle()
    {
        SceneController.Instance.LoadScene(titleSceneName);
    }

    // ── 진행 보고 API (퍼즐 측에서 호출) ──────────────────────
    /// <summary>1구역 자물쇠 해제 보고.</summary>
    public void UnlockGate1()
    {
        if (Gate1Unlocked) return;
        Gate1Unlocked = true;
        OnEscapeProgress?.Invoke(EscapeFlag.Gate1);
    }

    public void ObtainKeypad()
    {
        if (KeypadObtained) return;

        KeypadObtained = true;
        Debug.Log("[GameManager] Keypad obtained.", this);
        OnEscapeProgress?.Invoke(EscapeFlag.Keypad);
    }

    /// <summary>열쇠 획득 보고.</summary>
    public void ObtainKey()
    {
        if (KeyObtained) return;
        KeyObtained = true;
        OnEscapeProgress?.Invoke(EscapeFlag.Key);
    }

    /// <summary>2구역 탈출문 개방 보고.</summary>
    public void UnlockGate2()
    {
        if (Gate2Unlocked) return;
        Gate2Unlocked = true;
        OnEscapeProgress?.Invoke(EscapeFlag.Gate2);
    }

    // ── 내부 ─────────────────────────────────────────────────
    private void SetPhase(GamePhase phase)
    {
        if (CurrentPhase == phase) return;
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}
