using System.Collections.Generic;
using UnityEngine;

// Play 씬 사운드 연출을 연결한다. 클립은 AudioManager/ResourceManager가 Resources에서 이름으로 찾는다.
public class PlaySceneAudioController : MonoBehaviour
{
    private static PlaySceneAudioController _activeController;

    [Header("BGM")]
    [SerializeField] private string bgmName = "게임 배경음악";
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.1f;

    [Header("Player Footsteps")]
    [SerializeField] private Transform player;
    [SerializeField] private string continuousFootstepName = "발소리 연속";
    [SerializeField] private string[] footstepNames =
    {
        "발소리1", "발소리2", "발소리3", "발소리4",
        "발소리5", "발소리6", "발소리7", "발소리8"
    };
    [SerializeField] private float moveThreshold = 0.15f;
    [SerializeField] private float runThreshold = 7f;
    [SerializeField] private float walkStepInterval = 0.46f;
    [SerializeField] private float runStepInterval = 0.28f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.58f;
    [SerializeField, Range(0f, 1f)] private float continuousWalkVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float continuousRunVolume = 0.30f;

    [Header("Puangi")]
    [SerializeField] private PuangAI[] puangs;
    [SerializeField] private string puangAmbientName = "푸앙이 ambient sound";
    [SerializeField] private string puangStunReleaseName = "푸앙이 ambient sound 2";
    [SerializeField] private string puangStunName = "푸앙이 스턴";
    [SerializeField] private float puangAudibleRadius = 15f;
    [SerializeField, Range(0f, 1f)] private float puangIdleAmbientVolume = 0.825f;
    [SerializeField, Range(0f, 1f)] private float puangCuriousAmbientVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float puangChaseAmbientVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float puangStunVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float puangStunBoostVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float puangStunReleaseVolume = 1f;
    [SerializeField] private float puangAmbientGap = 2f;

    [Header("Horror Cue")]
    [SerializeField] private string horrorKickName = "호러 사운드 킥";
    [SerializeField, Range(0f, 1f)] private float horrorKickVolume = 0.78f;
    [SerializeField] private float horrorKickCooldown = 3f;

    [Header("Camera")]
    [SerializeField] private string cameraShotName = "카메라";
    [SerializeField, Range(0f, 1f)] private float cameraShotVolume = 1f;

    private class PuangAmbientState
    {
        public int ambientId = -1;
        public int stunId = -1;
        public int stunBoostId = -1;
        public int stunReleaseId = -1;
        public float nextAmbientPlayTime;
        public float stunReleaseOnlyUntil;
        public PuangAI.PuangState currentState = PuangAI.PuangState.Idle;
    }

    private readonly Dictionary<PuangAI, PuangAmbientState> _puangAmbientStates = new();
    private readonly Dictionary<PuangAI, System.Action<PuangAI.PuangState>> _puangStateHandlers = new();
    private readonly Dictionary<PuangAI, System.Action> _puangCaughtHandlers = new();

    private AudioManager _audio;
    private GameManager _gameManager;
    private Vector3 _lastPlayerPosition;
    private float _stepTimer;
    private float _lastHorrorKickTime = -999f;
    private int _footstepLoopId = -1;
    private int _lastFootstepIndex = -1;
    private bool _ownsAudio;

    private void Awake()
    {
        if (_activeController != null && _activeController != this)
        {
            enabled = false;
            return;
        }

        _activeController = this;
        _ownsAudio = true;
    }

    private void Start()
    {
        if (!_ownsAudio) return;

        _audio = AudioManager.Instance;
        BindReferences();
        SubscribeGameFlow();
        SubscribeCameraShot();
        StartSceneAudio();

        if (player != null)
            _lastPlayerPosition = player.position;
    }

    private void Update()
    {
        if (!_ownsAudio) return;

        UpdateFootsteps();
        UpdatePuangAmbiences();
    }

    private void OnDisable()
    {
        if (!_ownsAudio) return;

        StopFootstepLoop();
        StopPuangAmbiences();
        StopSceneBGM();
        UnsubscribePuangs();

        if (_gameManager != null)
            _gameManager.OnPhaseChanged -= OnPhaseChanged;

        CameraSystem.OnFlashFired -= OnCameraShot;
    }

    private void OnDestroy()
    {
        if (_activeController == this)
            _activeController = null;
    }

    private void BindReferences()
    {
        if (player == null)
        {
            var playerController = FindAnyObjectByType<Player_Ctrl>();
            if (playerController != null)
                player = playerController.transform;
            else
            {
                var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                    player = taggedPlayer.transform;
            }
        }

        if (puangs == null || puangs.Length == 0)
            puangs = FindObjectsByType<PuangAI>(FindObjectsInactive.Exclude);
    }

    private void SubscribeGameFlow()
    {
        _gameManager = GameManager.Current != null ? GameManager.Current : FindAnyObjectByType<GameManager>();
        if (_gameManager != null)
            _gameManager.OnPhaseChanged += OnPhaseChanged;
    }

    private void SubscribeCameraShot()
    {
        CameraSystem.OnFlashFired -= OnCameraShot;
        CameraSystem.OnFlashFired += OnCameraShot;
    }

    private void StartSceneAudio()
    {
        if (_audio == null) return;

        _audio.PlayBGM(bgmName, bgmVolume, true);
        StartPuangAmbiences();
    }

    private void StartPuangAmbiences()
    {
        if (_audio == null || puangs == null) return;

        foreach (var puang in puangs)
        {
            if (puang == null || _puangStateHandlers.ContainsKey(puang)) continue;

            _puangAmbientStates[puang] = new PuangAmbientState { currentState = puang.State };
            UpdatePuangAmbient(puang);

            System.Action<PuangAI.PuangState> stateHandler = state => OnPuangStateChanged(puang, state);
            System.Action caughtHandler = PlayHorrorKick;
            puang.OnStateChanged += stateHandler;
            puang.OnCaughtPlayer += caughtHandler;
            _puangStateHandlers[puang] = stateHandler;
            _puangCaughtHandlers[puang] = caughtHandler;
        }
    }

    private void UpdatePuangAmbiences()
    {
        if (_audio == null || _puangAmbientStates.Count == 0) return;

        foreach (var pair in _puangAmbientStates)
        {
            if (pair.Key == null) continue;

            UpdatePuangAmbient(pair.Key);
        }
    }

    private void UpdatePuangAmbient(PuangAI puang)
    {
        if (puang == null) return;
        if (!_puangAmbientStates.TryGetValue(puang, out var state)) return;

        bool audible = IsPuangAudible(puang);
        bool stunned = state.currentState == PuangAI.PuangState.Stun;
        bool playingStunRelease = Time.time < state.stunReleaseOnlyUntil;

        if (!audible || stunned || playingStunRelease)
        {
            StopPuangAmbient(state);
            return;
        }

        if (state.ambientId >= 0)
            _audio.SetSoundVolume(state.ambientId, GetPuangAmbientVolume(state.currentState));

        if (Time.time < state.nextAmbientPlayTime)
            return;

        StopPuangAmbient(state);
        state.ambientId = _audio.PlaySound(puangAmbientName, puang.transform, GetPuangAmbientVolume(state.currentState));

        var clip = ResourceManager.Instance.Get<AudioClip>(puangAmbientName);
        float clipLength = clip != null ? clip.length : 1f;
        state.nextAmbientPlayTime = Time.time + clipLength + Mathf.Max(0f, puangAmbientGap);
    }

    private bool IsPuangAudible(PuangAI puang)
    {
        if (player == null || puang == null)
            return false;

        float radius = Mathf.Max(0f, puangAudibleRadius);
        return (player.position - puang.transform.position).sqrMagnitude <= radius * radius;
    }

    private void StopPuangAmbient(PuangAmbientState state)
    {
        if (state == null || state.ambientId < 0)
            return;

        _audio.StopSound(state.ambientId);
        state.ambientId = -1;
        state.nextAmbientPlayTime = Time.time;
    }

    private void UpdateFootsteps()
    {
        if (_audio == null || player == null || Time.deltaTime <= 0f)
            return;

        Vector3 current = player.position;
        Vector3 previous = _lastPlayerPosition;
        current.y = 0f;
        previous.y = 0f;

        float speed = Vector3.Distance(current, previous) / Time.deltaTime;
        _lastPlayerPosition = player.position;

        bool moving = IsGameplayPhase() && speed >= moveThreshold;
        if (!moving)
        {
            StopFootstepLoop();
            _stepTimer = 0f;
            return;
        }

        float runRatio = Mathf.InverseLerp(moveThreshold, runThreshold, speed);
        float loopVolume = Mathf.Lerp(continuousWalkVolume, continuousRunVolume, runRatio);
        EnsureFootstepLoop(loopVolume);

        _stepTimer -= Time.deltaTime;
        if (_stepTimer > 0f)
            return;

        PlayFootstepVariant(runRatio);
        _stepTimer = Mathf.Lerp(walkStepInterval, runStepInterval, runRatio);
    }

    private bool IsGameplayPhase()
    {
        if (_gameManager == null)
            return true;

        return _gameManager.CurrentPhase == GamePhase.Playing || _gameManager.CurrentPhase == GamePhase.Escaping;
    }

    private void EnsureFootstepLoop(float volume)
    {
        if (_footstepLoopId < 0)
            _footstepLoopId = _audio.PlayLoopingSound(continuousFootstepName, player, volume);
        else
            _audio.SetSoundVolume(_footstepLoopId, volume);
    }

    private void StopFootstepLoop()
    {
        if (_audio == null || _footstepLoopId < 0) return;
        _audio.StopSound(_footstepLoopId);
        _footstepLoopId = -1;
    }

    private void PlayFootstepVariant(float runRatio)
    {
        if (footstepNames == null || footstepNames.Length == 0) return;

        int index = Random.Range(0, footstepNames.Length);
        if (footstepNames.Length > 1 && index == _lastFootstepIndex)
            index = (index + 1) % footstepNames.Length;

        _lastFootstepIndex = index;
        float volume = Mathf.Lerp(footstepVolume * 0.85f, footstepVolume, runRatio);
        _audio.PlaySound(footstepNames[index], player, volume);
    }

    private void OnPuangStateChanged(PuangAI puang, PuangAI.PuangState state)
    {
        if (puang == null) return;
        if (!_puangAmbientStates.TryGetValue(puang, out var ambientState)) return;

        var previousState = ambientState.currentState;
        ambientState.currentState = state;

        if (state == PuangAI.PuangState.Stun)
            PlayPuangStun(puang, ambientState);
        else if (previousState == PuangAI.PuangState.Stun)
            PlayPuangStunRelease(puang, ambientState);

        UpdatePuangAmbient(puang);

        if (state == PuangAI.PuangState.Chase)
            PlayHorrorKick();
    }

    private void PlayPuangStun(PuangAI puang, PuangAmbientState state)
    {
        StopPuangAmbient(state);
        StopPuangSound(state.stunId);
        StopPuangSound(state.stunBoostId);
        StopPuangSound(state.stunReleaseId);
        state.stunId = -1;
        state.stunBoostId = -1;
        state.stunReleaseId = -1;
        state.stunReleaseOnlyUntil = 0f;

        if (!IsPuangAudible(puang)) return;
        state.stunId = _audio.PlaySound(puangStunName, puang.transform, puangStunVolume);
        state.stunBoostId = _audio.PlaySound(puangStunName, puang.transform, puangStunBoostVolume);
    }

    private void PlayPuangStunRelease(PuangAI puang, PuangAmbientState state)
    {
        StopPuangSound(state.stunId);
        StopPuangSound(state.stunBoostId);
        StopPuangSound(state.stunReleaseId);
        state.stunId = -1;
        state.stunBoostId = -1;
        state.stunReleaseId = -1;

        if (!IsPuangAudible(puang))
        {
            state.stunReleaseOnlyUntil = 0f;
            return;
        }

        state.stunReleaseId = _audio.PlaySound(puangStunReleaseName, puang.transform, puangStunReleaseVolume);

        var clip = ResourceManager.Instance.Get<AudioClip>(puangStunReleaseName);
        state.stunReleaseOnlyUntil = Time.time + (clip != null ? clip.length : 1f);
    }

    private void StopPuangSound(int id)
    {
        if (_audio == null || id < 0) return;
        _audio.StopSound(id);
    }

    private void PlayHorrorKick()
    {
        if (_audio == null || Time.time < _lastHorrorKickTime + horrorKickCooldown)
            return;

        _lastHorrorKickTime = Time.time;
        _audio.PlayGlobalSound(horrorKickName, horrorKickVolume);
    }

    private float GetPuangAmbientVolume(PuangAI.PuangState state)
    {
        return state switch
        {
            PuangAI.PuangState.Curious => puangCuriousAmbientVolume,
            PuangAI.PuangState.Chase => puangChaseAmbientVolume,
            _ => puangIdleAmbientVolume
        };
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        if (phase is GamePhase.Ending or GamePhase.Cleared)
            StopFootstepLoop();
    }

    private void OnCameraShot(Vector3 flashPosition, float range)
    {
        if (_audio == null || string.IsNullOrEmpty(cameraShotName))
            return;

        _audio.PlayGlobalSound(cameraShotName, cameraShotVolume);
    }

    private void StopPuangAmbiences()
    {
        if (_audio == null) return;

        foreach (var state in _puangAmbientStates.Values)
        {
            StopPuangAmbient(state);
            StopPuangSound(state.stunId);
            StopPuangSound(state.stunBoostId);
            StopPuangSound(state.stunReleaseId);
        }

        _puangAmbientStates.Clear();
    }

    private void StopSceneBGM()
    {
        if (_audio == null) return;
        _audio.StopBGM(bgmName);
    }

    private void UnsubscribePuangs()
    {
        foreach (var pair in _puangStateHandlers)
        {
            if (pair.Key != null)
                pair.Key.OnStateChanged -= pair.Value;
        }

        foreach (var pair in _puangCaughtHandlers)
        {
            if (pair.Key != null)
                pair.Key.OnCaughtPlayer -= pair.Value;
        }

        _puangStateHandlers.Clear();
        _puangCaughtHandlers.Clear();
    }
}
