using UnityEngine;

[CreateAssetMenu(
    fileName = "SceneSO",
    menuName = "SceneSO")]
public class SceneSO : ScriptableObject
{

    [Header("씬 설정")]
    [Tooltip("전환 대상이 되는 메인 씬의 이름 (Build Settings 기준)")]
    public string targetSceneName;


    [Header("로딩 씬")]
    [Tooltip("메인 씬 로드 전에 표시할 로딩 씬 사용 여부")]
    public bool useLoadingScene = true;

    [Tooltip("로딩 씬의 이름 (Build Settings 기준). useLoadingScene 이 true 일 때만 사용됩니다.")]
    public string loadingSceneName;

    [Tooltip("로딩 완료 후 메인 씬 전환 전 대기 시간 (초). 0 이면 즉시 전환.")]
    [Min(0f)]
    public float leastHoldingDuration = 0f;


    [Header("전환 효과")]
    [Tooltip("씬 진입 시 사용할 전환 효과")]
    public TransitionEffect enterTransition = TransitionEffect.FadeIn;

    [Tooltip("씬 종료 시 사용할 전환 효과")]
    public TransitionEffect exitTransition = TransitionEffect.FadeOut;

    [Tooltip("전환 효과 재생 시간 (초)")]
    [Min(0f)]
    public float transitionDuration = 0.5f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
            Debug.LogWarning($"[SceneEntryConfig] '{name}' : targetSceneName 이 비어 있습니다.", this);

        if (useLoadingScene && string.IsNullOrWhiteSpace(loadingSceneName))
            Debug.LogWarning($"[SceneEntryConfig] '{name}' : useLoadingScene 이 true 이지만 loadingSceneName 이 비어 있습니다.", this);
    }
#endif
}

public enum TransitionEffect
{
    None = 0,
    FadeIn = 1,
    FadeOut = 2,
    SlideLeft = 3,
    SlideRight = 4,
    SlideUp = 5,
    SlideDown = 6,
    CrossFade = 7,
}