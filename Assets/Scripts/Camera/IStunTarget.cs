// IStunTarget.cs
// 기능: 카메라 플래시에 피격되어 스턴될 수 있는 대상(예: 푸앙이)이 구현하는 인터페이스.
// 사용법: 스턴 대상 컴포넌트에서 이 인터페이스를 구현하고 OnFlashStunned 안에서 자신의 상태를
//         STUN 으로 전환한다. CameraSystem 이 플래시 시 Raycast 로 대상을 찾아 호출한다.
public interface IStunTarget
{
    /// <summary>플래시에 피격되었을 때 호출. duration(초) 동안 스턴된다.</summary>
    void OnFlashStunned(float duration);
}
