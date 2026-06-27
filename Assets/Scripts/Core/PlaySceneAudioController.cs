using System.Collections.Generic;
using UnityEngine;

// Play 씬 사운드 연출을 연결한다. 클립은 AudioManager/ResourceManager가 Resources에서 이름으로 찾는다.
public class PlaySceneAudioController : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private string bgmName = "게임 배경음악";
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.45f;

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
    [SerializeField, Range(0f, 1f)] private float puangIdleAmbientVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float puangCuriousAmbientVolume = 0.48f;
    [SerializeField, Range(0f, 1f)] private float puangChaseAmbientVolume = 0.68f;
    [SerializeField, Range(0f, 1f)] private float puangStunAmbientVolume = 0.16f;

    [Header("Horror Cue")]
    [SerializeField] private string horrorKickName = "호러 사운드 킥";
    [SerializeField, Range(0f, 1f)] private float horrorKickVolume = 0.78f;
    [SerializeField] private float horrorKickCooldown = 3f;

    private readonly Dictionary<PuangAI, int> _puangAmbientIds = new();
    private readonly Dictionary<PuangAI, System.Action<PuangAI.PuangState>> _puangStateHandlers = new();
    private readonly Dictionary<PuangAI, System.Action> _puangCaughtHandlers = new();

    private AudioManager _audio;
    private GameManager _gameManager;
    private Vector3 _lastPlayerPosition;
    private float _stepTimer;
    private float _lastHorrorKickTime = -999f;
    private int _footstepLoopId = -1;
    private int _lastFootstepIndex = -1;

    private void Start()
    {
        _audio = AudioManager.Instance;
        BindReferences();
        SubscribeGameFlow();
        StartSceneAudio();

        if (player != null)
            _lastPlayerPosition = player.position;
    }

    private void Update()
    {
        UpdateFootsteps();
    }

    private void OnDisable()
    {
        StopFootstepLoop();
        StopPuangAmbiences();
        StopSceneBGM();
        UnsubscribePuangs();

        if (_gameManager != null)
            _gameManager.OnPhaseChanged -= OnPhaseChanged;
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

            int id = _audio.PlayLoopingSound(puangAmbientName, puang.transform, GetPuangAmbientVolume(puang.State));
            if (id >= 0)
                _puangAmbientIds[puang] = id;

            System.Action<PuangAI.PuangState> stateHandler = state => OnPuangStateChanged(puang, state);
            System.Action caughtHandler = PlayHorrorKick;
            puang.OnStateChanged += stateHandler;
            puang.OnCaughtPlayer += caughtHandler;
            _puangStateHandlers[puang] = stateHandler;
            _puangCaughtHandlers[puang] = caughtHandler;
        }
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

        if (_puangAmbientIds.TryGetValue(puang, out int id))
            _audio.SetSoundVolume(id, GetPuangAmbientVolume(state));

        if (state == PuangAI.PuangState.Chase)
            PlayHorrorKick();
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
            PuangAI.PuangState.Stun => puangStunAmbientVolume,
            _ => puangIdleAmbientVolume
        };
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        if (phase is GamePhase.Ending or GamePhase.Cleared)
            StopFootstepLoop();
    }

    private void StopPuangAmbiences()
    {
        if (_audio == null) return;

        foreach (int id in _puangAmbientIds.Values)
            _audio.StopSound(id);

        _puangAmbientIds.Clear();
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
