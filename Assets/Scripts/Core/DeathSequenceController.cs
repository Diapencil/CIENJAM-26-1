// DeathSequenceController.cs
// Feature: Runs the player death sequence after GameManager enters Dead.
// Usage: Attach to an in-game manager object that also has a UIDocument using DeathPanel.uxml
//        (must contain a 'death-video' VisualElement). Assign 3 death VideoClips (index 0/1/2)
//        or a VideoPlayer, and assign DeathPanelController.
//        The video is rendered to a runtime RenderTexture and shown as the 'death-video' element's
//        background (URP-safe; Camera Near/Far Plane render modes do not display under URP).
//        Any death source should call GameManager.Current.KillPlayer().
//        Clip selection: first death plays clip 0, later deaths play clip 1 or 2 at random
//        (based on GameManager.DeathCount, which persists across scene reloads).

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

public class DeathSequenceController : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("0번: 첫 사망 전용, 1·2번: 이후 사망 랜덤 재생 (3개 고정)")]
    [SerializeField] private List<VideoClip> deathClips = new List<VideoClip>();
    [SerializeField, Min(0f)] private float prepareTimeout = 3f;

    // 죽음 영상을 표시할 UI Toolkit 요소 이름 (DeathPanel.uxml). RenderTexture를 background로 출력.
    private const string VideoElementName = "death-video";

    [Header("Flow")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.6f;
    [SerializeField] private DeathPanelController deathPanel;
    [SerializeField] private Behaviour[] disableOnDeath;

    private bool _subscribed;
    private bool _running;

    private void Awake()
    {
        if (deathPanel == null)
            deathPanel = FindAnyObjectByType<DeathPanelController>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        deathPanel?.Hide();
    }

    private void OnDisable()
    {
        if (!_subscribed || GameManager.Current == null) return;
        GameManager.Current.OnPlayerDied -= HandlePlayerDied;
        _subscribed = false;
    }

    private void TrySubscribe()
    {
        if (_subscribed || GameManager.Current == null) return;
        GameManager.Current.OnPlayerDied += HandlePlayerDied;
        _subscribed = true;
        Debug.Log("[DeathSequenceController] Subscribed to GameManager.OnPlayerDied.", this);
    }

    private void HandlePlayerDied(GameManager.DeathContext context)
    {
        if (_running)
        {
            Debug.Log("[DeathSequenceController] Death sequence request ignored because it is already running.", this);
            return;
        }

        Debug.Log($"[DeathSequenceController] Death sequence started. reason='{context.Reason}' source='{context.Source}'", this);
        StartCoroutine(DeathRoutine(context));
    }

    private IEnumerator DeathRoutine(GameManager.DeathContext context)
    {
        _running = true;

        // 앨범(카메라) 서브모드의 시간 정지(timeScale 0)가 남아 있으면 페이드/연출 코루틴이 멈추므로 강제 복구.
        if (!Mathf.Approximately(Time.timeScale, 1f))
        {
            Debug.Log($"[DeathSequenceController] Restoring timeScale from {Time.timeScale:0.###} to 1 for death sequence.", this);
            Time.timeScale = 1f;
        }

        // 카메라(뷰파인더) 모드 중이면 1인칭으로 강제 복귀. (사망 영상이 비활성 fps 카메라 근평면에 출력돼 안 보이는 문제 방지)
        if (CameraController.Current != null && CameraController.Current.IsCameraView)
        {
            CameraController.Current.SetMode(CameraMode.FirstPerson);
            Debug.Log("[DeathSequenceController] Forced camera back to FirstPerson on death.", this);
        }

        DisableGameplayBehaviours();

        yield return PlayDeathVideo();

        if (ScreenFader.Instance != null)
        {
            Debug.Log($"[DeathSequenceController] Fade out started. duration={fadeOutDuration:0.###}", this);
            yield return ScreenFader.Instance.FadeOut(fadeOutDuration).WaitForCompletion();
            Debug.Log("[DeathSequenceController] Fade out completed.", this);
            ScreenFader.Instance.SetInstant(0f);
            Debug.Log("[DeathSequenceController] ScreenFader cleared before showing death panel.", this);
        }
        else
        {
            Debug.LogWarning("[DeathSequenceController] ScreenFader is missing. Showing death panel without fadeout.", this);
        }

        if (deathPanel != null)
        {
            deathPanel.Show(context);
            Debug.Log("[DeathSequenceController] Death panel shown.", this);
        }
        else
        {
            Debug.LogWarning("[DeathSequenceController] DeathPanelController is missing. Death flow reached final state without UI.", this);
        }
    }

    private void DisableGameplayBehaviours()
    {
        if (disableOnDeath == null) return;

        foreach (Behaviour target in disableOnDeath)
        {
            if (target == null || target == this || target == deathPanel) continue;
            target.enabled = false;
            Debug.Log($"[DeathSequenceController] Disabled behaviour on death: {target.GetType().Name} ({target.name})", target);
        }
    }

    private IEnumerator PlayDeathVideo()
    {
        VideoClip clip = SelectDeathClip();
        VideoPlayer player = ResolveVideoPlayer(clip);
        if (player == null)
        {
            Debug.Log("[DeathSequenceController] Death video skipped: no VideoPlayer and no death clip assigned.", this);
            yield break;
        }

        if (player.clip == null)
        {
            Debug.Log("[DeathSequenceController] Death video skipped: VideoPlayer has no clip assigned.", player);
            yield break;
        }

        // URP에서는 CameraNearPlane이 그려지지 않으므로 RenderTexture에 렌더해 UI Toolkit 요소에 출력한다.
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
        player.waitForFirstFrame = true;

        Debug.Log($"[DeathSequenceController] Death video prepare started. clip='{player.clip.name}' size={width}x{height}", player);
        player.Prepare();
        float startedAt = Time.unscaledTime;
        while (!player.isPrepared && Time.unscaledTime - startedAt < prepareTimeout)
            yield return null;

        if (!player.isPrepared)
        {
            Debug.LogWarning($"[DeathSequenceController] Death video skipped: prepare timed out after {prepareTimeout:0.###} seconds.", player);
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
            Debug.LogWarning($"[DeathSequenceController] '{VideoElementName}' element not found in UIDocument; video has no display target.", this);
        }

        Debug.Log("[DeathSequenceController] Death video playback started.", player);
        player.Play();
        yield return null;

        // 진단: 표시 요소가 실제로 레이아웃/패널에 올라갔는지 확인
        if (videoElement != null)
        {
            var rs = videoElement.resolvedStyle;
            Debug.Log($"[DeathSequenceController][diag] death-video display={rs.display} visibility={rs.visibility} " +
                      $"w={rs.width:0} h={rs.height:0} panel={(videoElement.panel != null)} " +
                      $"hasRT={(rs.backgroundImage.renderTexture != null)} " +
                      $"rootDisplay={(videoElement.parent != null ? videoElement.parent.resolvedStyle.display.ToString() : "null")}", this);
        }

        while (player != null && player.isPlaying)
        {
            // UITK는 요소 속성 변경 시에만 리페인트하므로, RenderTexture 내용 갱신을 반영하려면 매 프레임 강제 리페인트.
            videoElement?.MarkDirtyRepaint();
            yield return null;
        }

        Debug.Log("[DeathSequenceController] Death video playback completed.", this);
        ReleaseRenderTexture(player, renderTexture, videoElement);
    }

    // RenderTexture/표시 요소를 정리한다. (재생 종료 또는 prepare 실패 시 공통 호출)
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
        UIDocument document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
        {
            Debug.LogWarning("[DeathSequenceController] UIDocument is missing; cannot display death video.", this);
            return null;
        }

        return document.rootVisualElement.Q<VisualElement>(VideoElementName);
    }

    // 사망 횟수에 따라 재생할 클립 선택.
    // 첫 사망(DeathCount<=1) → 0번, 이후 → 1·2번 랜덤. 리스트는 3개 고정 전제.
    private VideoClip SelectDeathClip()
    {
        if (deathClips == null || deathClips.Count < 3)
        {
            Debug.LogWarning("[DeathSequenceController] deathClips must contain 3 clips (index 0/1/2).", this);
            return null;
        }

        int index = GameManager.DeathCount <= 1 ? 0 : Random.Range(1, 3);
        Debug.Log($"[DeathSequenceController] Selected death clip index={index}. deathCount={GameManager.DeathCount}", this);
        return deathClips[index];
    }

    private VideoPlayer ResolveVideoPlayer(VideoClip clip)
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null && clip != null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            Debug.Log("[DeathSequenceController] Added VideoPlayer for assigned death clip.", this);
        }

        if (videoPlayer == null)
            return null;

        if (clip != null)
            videoPlayer.clip = clip;

        // 실제 렌더 설정(RenderTexture 모드/타겟)은 PlayDeathVideo에서 수행한다.
        return videoPlayer;
    }
}
