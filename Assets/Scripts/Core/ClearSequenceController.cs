// ClearSequenceController.cs
// Feature: Runs the clear/ending sequence after GameManager enters the Ending phase
//          (DoorLockKeypad calls GameManager.TriggerEnding on success).
//          Plays an optional ending VideoClip through a UI Toolkit RenderTexture target
//          (URP-safe), fades out, shows the clear panel, then confirms the clear (CompleteClear -> Cleared).
// Usage: Attach to an in-game manager object. Assign a VideoClip/VideoPlayer (optional),
//        ClearPanelController, and any gameplay Behaviours to disable on clear.

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

public class ClearSequenceController : MonoBehaviour
{
    [Header("Video (optional)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private VideoClip clearClip;
    [SerializeField, Min(0f)] private float prepareTimeout = 3f;

    // 클리어 영상을 표시할 UI Toolkit 요소 이름 (ClearPanel.uxml). RenderTexture를 background로 출력.
    private const string VideoElementName = "clear-video";

    [Header("Flow")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.6f;
    [SerializeField] private ClearPanelController clearPanel;
    [SerializeField] private Behaviour[] disableOnClear;

    private bool _subscribed;
    private bool _running;

    private void Awake()
    {
        if (clearPanel == null)
            clearPanel = FindAnyObjectByType<ClearPanelController>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        clearPanel?.Hide();
    }

    private void OnDisable()
    {
        if (!_subscribed || GameManager.Current == null) return;
        GameManager.Current.OnPhaseChanged -= HandlePhaseChanged;
        _subscribed = false;
    }

    private void TrySubscribe()
    {
        if (_subscribed || GameManager.Current == null) return;
        GameManager.Current.OnPhaseChanged += HandlePhaseChanged;
        _subscribed = true;
        Debug.Log("[ClearSequenceController] Subscribed to GameManager.OnPhaseChanged.", this);
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase != GamePhase.Ending) return;
        if (_running)
        {
            Debug.Log("[ClearSequenceController] Clear sequence request ignored because it is already running.", this);
            return;
        }

        Debug.Log("[ClearSequenceController] Clear sequence started.", this);
        StartCoroutine(ClearRoutine());
    }

    private IEnumerator ClearRoutine()
    {
        _running = true;
        DisableGameplayBehaviours();

        yield return PlayClearVideo();

        if (ScreenFader.Instance != null)
        {
            yield return ScreenFader.Instance.FadeOut(fadeOutDuration).WaitForCompletion();
            ScreenFader.Instance.SetInstant(0f);
        }
        else
        {
            Debug.LogWarning("[ClearSequenceController] ScreenFader is missing. Showing clear panel without fadeout.", this);
        }

        if (clearPanel != null)
        {
            clearPanel.Show();
            Debug.Log("[ClearSequenceController] Clear panel shown.", this);
        }
        else
        {
            Debug.LogWarning("[ClearSequenceController] ClearPanelController is missing. Clear flow reached final state without UI.", this);
        }

        if (GameManager.Current != null)
            GameManager.Current.CompleteClear();
    }

    private void DisableGameplayBehaviours()
    {
        if (disableOnClear == null) return;

        foreach (Behaviour target in disableOnClear)
        {
            if (target == null || target == this || target == clearPanel) continue;
            target.enabled = false;
            Debug.Log($"[ClearSequenceController] Disabled behaviour on clear: {target.GetType().Name} ({target.name})", target);
        }
    }

    private IEnumerator PlayClearVideo()
    {
        VideoPlayer player = ResolveVideoPlayer();
        if (player == null)
        {
            Debug.Log("[ClearSequenceController] Ending video skipped: no VideoPlayer and no clear clip assigned.", this);
            yield break;
        }

        if (player.clip == null)
        {
            Debug.Log("[ClearSequenceController] Ending video skipped: VideoPlayer has no clip assigned.", player);
            yield break;
        }

        VideoClip clip = player.clip;
        VisualElement videoElement = ResolveVideoElement();
        int width = clip.width > 0 ? (int)clip.width : Screen.width;
        int height = clip.height > 0 ? (int)clip.height : Screen.height;
        RenderTexture renderTexture = new RenderTexture(width, height, 0);
        renderTexture.Create();

        player.Stop();
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = renderTexture;
        player.isLooping = false;
        player.playOnAwake = false;
        // RenderTexture 출력은 첫 프레임 대기 중 멈출 수 있어 DeathSequenceController와 동일하게 끈다.
        player.waitForFirstFrame = false;

        Debug.Log($"[ClearSequenceController] Ending video prepare started. clip='{clip.name}' size={width}x{height}", player);
        player.Prepare();
        float startedAt = Time.unscaledTime;
        while (!player.isPrepared && Time.unscaledTime - startedAt < prepareTimeout)
            yield return null;

        if (!player.isPrepared)
        {
            Debug.LogWarning($"[ClearSequenceController] Ending video skipped: prepare timed out after {prepareTimeout:0.###}s.", player);
            ReleaseRenderTexture(player, renderTexture, videoElement);
            yield break;
        }

        if (videoElement != null)
        {
            videoElement.style.backgroundImage = Background.FromRenderTexture(renderTexture);
            videoElement.style.display = DisplayStyle.Flex;
            videoElement.BringToFront();
        }
        else
        {
            Debug.LogWarning($"[ClearSequenceController] '{VideoElementName}' element not found in ClearPanel UIDocument; video has no display target.", this);
        }

        Debug.Log("[ClearSequenceController] Ending video playback started.", player);
        player.Play();
        yield return null;

        while (player != null && player.isPlaying)
        {
            videoElement?.MarkDirtyRepaint();
            yield return null;
        }

        Debug.Log("[ClearSequenceController] Ending video playback completed.", this);
        ReleaseRenderTexture(player, renderTexture, videoElement);
    }

    private void ReleaseRenderTexture(VideoPlayer player, RenderTexture renderTexture, VisualElement videoElement)
    {
        if (videoElement != null)
        {
            videoElement.style.display = DisplayStyle.None;
            videoElement.style.backgroundImage = StyleKeyword.None;
        }

        if (player != null && player.targetTexture == renderTexture)
            player.targetTexture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private VisualElement ResolveVideoElement()
    {
        if (clearPanel == null)
        {
            Debug.LogWarning("[ClearSequenceController] ClearPanelController is missing; cannot display ending video.", this);
            return null;
        }

        UIDocument document = clearPanel.GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
        {
            Debug.LogWarning("[ClearSequenceController] ClearPanel UIDocument is missing; cannot display ending video.", clearPanel);
            return null;
        }

        if (document.sortingOrder < 6000)
            document.sortingOrder = 6000;

        return document.rootVisualElement.Q<VisualElement>(VideoElementName);
    }

    private VideoPlayer ResolveVideoPlayer()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null && clearClip != null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            Debug.Log("[ClearSequenceController] Added VideoPlayer for assigned ending clip.", this);
        }

        if (videoPlayer == null)
            return null;

        if (clearClip != null)
            videoPlayer.clip = clearClip;

        return videoPlayer;
    }
}
