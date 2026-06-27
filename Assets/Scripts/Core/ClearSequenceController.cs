// ClearSequenceController.cs
// Feature: Runs the clear/ending sequence after GameManager enters the Ending phase
//          (DoorLockKeypad calls GameManager.TriggerEnding on success).
//          Plays an optional ending VideoClip (skipped if none), fades out, shows the
//          clear panel, then confirms the clear (CompleteClear -> Cleared).
// Usage: Attach to an in-game manager object. Assign a VideoClip/VideoPlayer (optional),
//        ClearPanelController, and any gameplay Behaviours to disable on clear.

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Video;

public class ClearSequenceController : MonoBehaviour
{
    [Header("Video (optional)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private VideoClip clearClip;
    [SerializeField, Min(0f)] private float prepareTimeout = 3f;

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
        if (player == null || player.clip == null)
        {
            Debug.Log("[ClearSequenceController] Ending video skipped: no clip assigned.", this);
            yield break;
        }

        player.Stop();
        player.isLooping = false;
        player.playOnAwake = false;
        player.waitForFirstFrame = true;

        player.Prepare();
        float startedAt = Time.unscaledTime;
        while (!player.isPrepared && Time.unscaledTime - startedAt < prepareTimeout)
            yield return null;

        if (!player.isPrepared)
        {
            Debug.LogWarning($"[ClearSequenceController] Ending video skipped: prepare timed out after {prepareTimeout:0.###}s.", player);
            yield break;
        }

        player.Play();
        yield return null;

        while (player != null && player.isPlaying)
            yield return null;

        Debug.Log("[ClearSequenceController] Ending video playback completed.", this);
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

        if (videoPlayer.renderMode == VideoRenderMode.APIOnly)
        {
            videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            videoPlayer.targetCamera = Camera.main;
        }

        return videoPlayer;
    }
}
