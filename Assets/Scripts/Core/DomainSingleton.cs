using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 도메인(씬/컨텍스트) 스코프 싱글톤. 글로벌 Singleton&lt;T&gt;와 달리 DontDestroyOnLoad 하지 않으며,
/// 자신이 속한 GameObject가 파괴될 때 함께 소멸한다(스코프 종료 시 Current=null).
/// 인게임/아웃게임 도메인을 엄격히 분리하기 위한 베이스.
/// </summary>
public abstract class DomainSingleton<T> : MonoBehaviour where T : DomainSingleton<T>
{
    public static T Current { get; private set; }

    protected virtual void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 중복 인스턴스 — 새로 생성된 것을 파괴합니다.", this);
            Destroy(this);
            return;
        }
        Current = (T)this;
    }

    protected virtual void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
