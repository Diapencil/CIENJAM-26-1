// DeathSequenceController.cs
// Feature: Runs the player death sequence after GameManager enters Dead.
// Usage: Attach to an in-game manager object, assign a death VideoClip or VideoPlayer,
//        and assign DeathPanelController. Any death source should call GameManager.Current.KillPlayer().

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Video;

public class DeathSequenceController : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private VideoClip deathClip;
    [SerializeField] private Camera targetCamera; // 비우면 Camera.main 사용
    [SerializeField, Min(0f)] private float prepareTimeout = 3f;

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
        VideoPlayer player = ResolveVideoPlayer();
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

        player.Stop();
        player.isLooping = false;
        player.playOnAwake = false;
        player.waitForFirstFrame = true;

        Debug.Log($"[DeathSequenceController] Death video prepare started. clip='{player.clip.name}'", player);
        player.Prepare();
        float startedAt = Time.unscaledTime;
        while (!player.isPrepared && Time.unscaledTime - startedAt < prepareTimeout)
            yield return null;

        if (!player.isPrepared)
        {
            Debug.LogWarning($"[DeathSequenceController] Death video skipped: prepare timed out after {prepareTimeout:0.###} seconds.", player);
            yield break;
        }

        Debug.Log("[DeathSequenceController] Death video playback started.", player);
        player.Play();
        yield return null;

        while (player != null && player.isPlaying)
            yield return null;

        Debug.Log("[DeathSequenceController] Death video playback completed.", this);
    }

    private VideoPlayer ResolveVideoPlayer()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null && deathClip != null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            Debug.Log("[DeathSequenceController] Added VideoPlayer for assigned death clip.", this);
        }

        if (videoPlayer == null)
            return null;

        if (deathClip != null)
            videoPlayer.clip = deathClip;

        // 항상 카메라 근평면 출력으로 강제 (소리만 나고 화면 안 나오는 문제 방지)
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;

        if (videoPlayer.targetCamera == null)
            videoPlayer.targetCamera = targetCamera != null ? targetCamera : Camera.main;

        if (videoPlayer.targetCamera == null)
            Debug.LogWarning("[DeathSequenceController] No target camera for death video. " +
                             "Assign Camera in inspector or tag the game camera as MainCamera.", this);

        return videoPlayer;
    }
}
