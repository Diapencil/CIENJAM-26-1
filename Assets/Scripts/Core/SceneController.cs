using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : Singleton<SceneController>
{

    [Header("씬 설정 목록")]
    [Tooltip("프로젝트에서 사용하는 모든 SceneSO 를 등록하세요.")]
    [SerializeField] private List<SceneSO> sceneConfigs;

    public static float LoadingProgress { get; private set; }

    public static bool IsTransitioning { get; private set; }


    private Dictionary<string, SceneSO> _configMap;
    private readonly List<ISceneEventListener> _listeners = new();


    new protected void Awake()
    {
        base.Awake();
        BuildConfigMap();
        ScreenFader.Instance.SetInstant(0f);
    }

    /// <summary>SceneSO 리스트를 딕셔너리로 변환합니다.</summary>
    private void BuildConfigMap()
    {
        _configMap = new Dictionary<string, SceneSO>();

        foreach (var config in sceneConfigs)
        {
            if (config == null) continue;

            if (string.IsNullOrEmpty(config.targetSceneName))
            {
                Debug.LogWarning($"[SceneController] targetSceneName 이 비어 있는 config 가 있습니다: '{config.name}'");
                continue;
            }

            if (_configMap.ContainsKey(config.targetSceneName))
            {
                Debug.LogWarning($"[SceneController] 중복된 targetSceneName: '{config.targetSceneName}'. 첫 번째 항목만 사용합니다.");
                continue;
            }

            _configMap[config.targetSceneName] = config;
        }
    }

    public void LoadScene(string sceneName)
    {
        if (IsTransitioning)
        {
            Debug.LogWarning($"[SceneController] 전환 중입니다. '{sceneName}' 요청을 무시합니다.");
            return;
        }

        if (!_configMap.TryGetValue(sceneName, out var config))
        {
            Debug.LogError($"[SceneController] '{sceneName}' 에 해당하는 SceneSO 가 없습니다.");
            return;
        }

        StartCoroutine(LoadSceneRoutine(config));
    }

    public void RegisterListener(ISceneEventListener listener)
    {
        if (listener == null || _listeners.Contains(listener)) return;
        _listeners.Add(listener);
    }

    public void UnregisterListener(ISceneEventListener listener)
    {
        _listeners.Remove(listener);
    }


    private IEnumerator LoadSceneRoutine(SceneSO config)
    {
        IsTransitioning = true;
        LoadingProgress = 0f;
        // ── 1. 이벤트: 씬 로드 시작 ──
        NotifyLoadStart(config.targetSceneName);
        //DataManager.Instance.SaveGame();


        // ── 2. 현재 씬 퇴장 전환 효과 ──
        yield return StartCoroutine(PlayTransition(config.exitTransition, config.transitionDuration));

        // ── 3. 로딩씬 전환 ──
        if (config.useLoadingScene && !string.IsNullOrEmpty(config.loadingSceneName))
        {
            yield return SceneManager.LoadSceneAsync(config.loadingSceneName, LoadSceneMode.Single);

            // 로딩씬 진입 전환 효과 (화면을 다시 보여줌)
            yield return StartCoroutine(PlayTransition(config.enterTransition, config.transitionDuration));
        }

        // ── 4. 목표 씬 비동기 로드 (즉시 활성화 X) ──
        var asyncOp = SceneManager.LoadSceneAsync(config.targetSceneName, LoadSceneMode.Single);
        asyncOp.allowSceneActivation = false;

        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;

            // AsyncOperation 은 allowSceneActivation = false 일 때 0.9 에서 멈춤
            LoadingProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            bool loadDone = asyncOp.progress >= 0.9f;
            bool holdDone = elapsed >= config.leastHoldingDuration;

            if (loadDone && holdDone) break;

            yield return null;
        }

        LoadingProgress = 1f;

        if (config.useLoadingScene && !string.IsNullOrEmpty(config.loadingSceneName))
            yield return StartCoroutine(PlayTransition(config.exitTransition, config.transitionDuration));

        asyncOp.allowSceneActivation = true;
        yield return asyncOp;

        //DataManager.Instance.LoadGame();
        yield return StartCoroutine(PlayTransition(config.enterTransition, config.transitionDuration));

        IsTransitioning = false;
        NotifyLoadComplete(config.targetSceneName);
    }


    private IEnumerator PlayTransition(TransitionEffect effect, float duration)
    {
        if (effect == TransitionEffect.None || duration <= 0f)
            yield break;

        switch (effect)
        {
            case TransitionEffect.FadeIn:
                yield return ScreenFader.Instance.FadeIn(duration).WaitForCompletion();
                break;

            case TransitionEffect.FadeOut:
                yield return ScreenFader.Instance.FadeOut(duration).WaitForCompletion();
                break;

            case TransitionEffect.CrossFade:
                yield return ScreenFader.Instance.FadeOut(duration * 0.5f).WaitForCompletion();
                yield return ScreenFader.Instance.FadeIn(duration * 0.5f).WaitForCompletion();
                break;

            // TODO: Slide 계열 효과는 추후 RectTransform 애니메이션으로 구현
            case TransitionEffect.SlideLeft:
            case TransitionEffect.SlideRight:
            case TransitionEffect.SlideUp:
            case TransitionEffect.SlideDown:
                Debug.LogWarning($"[SceneController] '{effect}' 효과는 아직 구현되지 않았습니다. FadeIn/Out 으로 대체합니다.");
                yield return ScreenFader.Instance.FadeOut(duration).WaitForCompletion();
                break;
        }
    }


    private void NotifyLoadStart(string sceneName)
    {
        var snapshot = new List<ISceneEventListener>(_listeners);
        foreach (var listener in snapshot)
            listener.OnSceneLoadStart(sceneName);
    }

    private void NotifyLoadComplete(string sceneName)
    {
        var snapshot = new List<ISceneEventListener>(_listeners);
        foreach (var listener in snapshot)
            listener.OnSceneLoadComplete(sceneName);
    }
}


public interface ISceneEventListener
{
    public void OnSceneLoadStart(string sceneName);
    public void OnSceneLoadComplete(string sceneName);
}
