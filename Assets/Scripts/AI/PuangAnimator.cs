// PuangAnimator.cs
// 기능: 푸앙이의 FSM 상태(PuangAI.PuangState)를 Animator 의 IsWalk(Bool) 파라미터로 연결한다.
//       이동 상태(Wander / Curious / Chase)면 IsWalk=true, 정지 상태(Idle / Stun)면 IsWalk=false 로 설정해
//       Idle ↔ Walk 애니메이션을 자동 전환시킨다. PuangAI 본체는 수정하지 않고 OnStateChanged 이벤트만 구독한다.
// 사용법: 푸앙이 GameObject(PuangAI + Animator 포함)에 본 컴포넌트를 추가한다. Animator 의 컨트롤러는
//         Puangi_Walk_Polished(IsWalk Bool 파라미터 보유)여야 한다. animator / puangAI 미할당 시 같은
//         GameObject 에서 자동 탐색하며, isWalkParam 으로 파라미터 이름을 바꿀 수 있다.

using UnityEngine;

[RequireComponent(typeof(PuangAI))]
public class PuangAnimator : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("제어할 Animator. 미할당 시 같은 GameObject 또는 자식에서 자동 탐색")]
    [SerializeField] private Animator animator;
    [Tooltip("상태를 받아올 PuangAI. 미할당 시 같은 GameObject 에서 자동 탐색")]
    [SerializeField] private PuangAI puangAI;

    [Header("파라미터")]
    [Tooltip("이동 여부를 전달할 Animator Bool 파라미터 이름")]
    [SerializeField] private string isWalkParam = "IsWalk";

    private int _isWalkHash;

    private void Awake()
    {
        if (puangAI == null) puangAI = GetComponent<PuangAI>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _isWalkHash = Animator.StringToHash(isWalkParam);
    }

    private void OnEnable()
    {
        if (puangAI != null) puangAI.OnStateChanged += HandleStateChanged;
        // 현재 상태로 초기 동기화
        if (puangAI != null) HandleStateChanged(puangAI.State);
    }

    private void OnDisable()
    {
        if (puangAI != null) puangAI.OnStateChanged -= HandleStateChanged;
    }

    // 이동 상태면 걷기, 정지 상태면 멈춤.
    private void HandleStateChanged(PuangAI.PuangState state)
    {
        if (animator == null) return;
        bool walking = state == PuangAI.PuangState.Wander
                    || state == PuangAI.PuangState.Curious
                    || state == PuangAI.PuangState.Chase;
        animator.SetBool(_isWalkHash, walking);
    }
}
