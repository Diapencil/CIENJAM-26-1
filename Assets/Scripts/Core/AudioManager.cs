using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// 오디오 매니저. SFX 채널 풀 + BGM/앰비언스 레이어(이름별, 중첩). 볼륨은 AudioMixer 그룹(Master/SFX/BGM)으로 제어한다.
// 사용법:
//   AudioManager.Instance.PlaySound("Hit", transform, 1f, 1);  // SFX 1회 재생, id 반환
//   AudioManager.Instance.StopSound(id);                       // 재생 중 SFX 중단/반환
//   AudioManager.Instance.PlayBGM("MenuTheme");                // BGM 루프(이름별 레이어, 중첩 가능)
//   AudioManager.Instance.PlayBGM("Rain01");                   // 위와 동시에 깔림(전역 앰비언스)
//   AudioManager.Instance.StopBGM("Rain01");                   // 특정 레이어만 정지
//   AudioManager.Instance.MasterVolume = 0.5f;                 // 믹서에 즉시 반영(라이브)
// 에디터 셋업:
//   - AudioMixer 에셋(Master > SFX, BGM 그룹)을 만들고 각 그룹 Volume 을 스크립트로 노출.
//   - 노출 파라미터 이름은 아래 상수(MasterParam/SfxParam/BgmParam)와 정확히 일치해야 한다.
//   - 인스펙터에 mixer / sfxGroup / bgmGroup 지정. (SFX 채널은 코드에서 생성하므로 프리팹 불필요)
public class AudioManager : Singleton<AudioManager>, ISceneEventListener
{
    [Header("Mixer")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup bgmGroup;

    // 믹서 에셋에서 동일한 이름으로 노출해야 하는 파라미터 키
    const string MasterParam = "MasterVolume";
    const string SfxParam = "SFXVolume";
    const string BgmParam = "BGMVolume";

    [Header("SFX")]
    [SerializeField, Range(0f, 1f)] float spatialBlend = 1f; // 0 = 2D, 1 = 3D(위치 기반)
    [SerializeField] float minDistance = 1f;
    [SerializeField] float maxDistance = 15f;

    [Header("Volume (0..1)")]
    [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] float bgmVolume = 1f;

    // set 시 믹서에 dB 로 즉시 반영 → 재생 중인 SFX/BGM 전부 라이브 적용된다.
    public float MasterVolume
    {
        get => masterVolume;
        set { masterVolume = Mathf.Clamp01(value); ApplyVolume(MasterParam, masterVolume); }
    }
    public float SFXVolume
    {
        get => sfxVolume;
        set { sfxVolume = Mathf.Clamp01(value); ApplyVolume(SfxParam, sfxVolume); }
    }
    public float BGMVolume
    {
        get => bgmVolume;
        set { bgmVolume = Mathf.Clamp01(value); ApplyVolume(BgmParam, bgmVolume); }
    }

    // 재생 중 채널 상태. remaining = 남은 반복 횟수.
    struct ChannelState
    {
        public AudioSource source;
        public int remaining;
    }

    readonly Dictionary<string, AudioClip> _clips = new();
    readonly Dictionary<int, ChannelState> _active = new();
    readonly List<int> _toRemove = new();   // Update 재사용(매 프레임 할당 방지)
    readonly List<int> _toReplay = new();

    readonly Dictionary<string, AudioSource> _bgmLayers = new(); // 이름별 BGM/앰비언스 루프 소스(중첩)

    Transform _poolParent;
    int _nextId = 1;

    new void Awake()
    {
        base.Awake();
        _poolParent = transform;

        LoadAllSounds();

        // 직렬화된 초기 볼륨을 믹서에 반영
        ApplyVolume(MasterParam, masterVolume);
        ApplyVolume(SfxParam, sfxVolume);
        ApplyVolume(BgmParam, bgmVolume);

        // 이전 버전에서 누락됐던 등록 — 씬 전환 시 OnSceneLoadStart 가 호출되도록.
        SceneController.Instance.RegisterListener(this);
    }

    void Update()
    {
        if (_active.Count == 0) return;

        _toRemove.Clear();
        _toReplay.Clear();

        foreach (var kv in _active)
        {
            var st = kv.Value;
            if (st.source == null) { _toRemove.Add(kv.Key); continue; }
            if (st.source.isPlaying) continue;

            if (st.remaining > 1) _toReplay.Add(kv.Key);
            else _toRemove.Add(kv.Key);
        }

        for (int i = 0; i < _toReplay.Count; i++)
        {
            int id = _toReplay[i];
            var st = _active[id];
            st.remaining--;
            _active[id] = st;
            st.source.Play();
        }

        for (int i = 0; i < _toRemove.Count; i++)
            ReturnChannel(_toRemove[i]);
    }

    // ── SFX ───────────────────────────────────────────────────────
    // volume: 사운드별 기준 볼륨(0..1). Master/SFX 감쇠는 믹서가 처리한다.
    // repeatTime: 재생 횟수(최소 1). 반환 id 로 StopSound 가능.
    public int PlaySound(string soundName, Transform sourceParent, float volume = 1f, int repeatTime = 1)
    {
        if (!_clips.TryGetValue(soundName, out var clip)) return -1;

        var ch = GetChannel();
        if (ch == null) return -1;

        ch.transform.SetParent(sourceParent, false);
        ch.transform.localPosition = Vector3.zero;
        ch.clip = clip;
        ch.volume = Mathf.Clamp01(volume);
        ch.loop = false;
        ch.Play();

        int id = _nextId++;
        _active[id] = new ChannelState { source = ch, remaining = Mathf.Max(1, repeatTime) };
        return id;
    }

    public void StopSound(int id) => ReturnChannel(id);

    AudioSource GetChannel()
    {
        // 풀(자식)에서 비활성 채널 재사용
        for (int i = 0; i < _poolParent.childCount; i++)
        {
            var c = _poolParent.GetChild(i).GetComponent<AudioSource>();
            if (c != null && !c.gameObject.activeSelf)
            {
                c.gameObject.SetActive(true);
                return c;
            }
        }

        return CreateChannel();
    }

    // SFX 채널을 코드로 생성한다(프리팹 불필요). 설정은 전부 여기서 일괄 적용.
    AudioSource CreateChannel()
    {
        var go = new GameObject("SFXChannel");
        go.transform.SetParent(_poolParent, false);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.outputAudioMixerGroup = sfxGroup;
        src.spatialBlend = spatialBlend;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        return src;
    }

    void ReturnChannel(int id)
    {
        if (!_active.TryGetValue(id, out var st)) return;

        if (st.source != null)
        {
            st.source.Stop();
            st.source.clip = null;
            st.source.transform.SetParent(_poolParent, false);
            st.source.gameObject.SetActive(false);
        }
        _active.Remove(id);
    }

    // ── BGM / 앰비언스 (이름별 레이어, 중첩 재생) ─────────────────
    // volume: 트랙 기준 볼륨(0..1). Master/BGM 감쇠는 믹서가 처리한다.
    // 서로 다른 이름은 동시에 깔린다(메인 음악 + 비/바람 등 전역 앰비언스).
    // 같은 이름이 이미 재생 중이면 재시작하지 않고 볼륨만 갱신한다(씬 재진입 시 끊김 방지).
    public void PlayBGM(string soundName, float volume = 1f, bool loop = true)
    {
        if (!_clips.TryGetValue(soundName, out var clip))
        {
            Debug.LogWarning($"[AudioManager] BGM 클립을 찾을 수 없음: {soundName}");
            return;
        }

        var src = GetOrCreateBgmLayer(soundName);
        src.loop = loop;
        src.volume = Mathf.Clamp01(volume);

        if (src.clip == clip && src.isPlaying) return; // 이미 재생 중이면 볼륨만 갱신

        src.clip = clip;
        src.Play();
    }

    // 특정 레이어만 정지. soundName 생략 시 전체 정지.
    public void StopBGM(string soundName)
    {
        if (_bgmLayers.TryGetValue(soundName, out var src) && src != null) src.Stop();
    }

    public void StopALlBGM()
    {
        foreach (var src in _bgmLayers.Values)
            if (src != null) src.Stop();
    }

    AudioSource GetOrCreateBgmLayer(string soundName)
    {
        if (_bgmLayers.TryGetValue(soundName, out var src) && src != null) return src;

        var go = new GameObject($"BGM_{soundName}");
        go.transform.SetParent(_poolParent, false);

        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f; // 2D 전역
        src.outputAudioMixerGroup = bgmGroup;

        _bgmLayers[soundName] = src;
        return src;
    }

    // ── 볼륨 → 믹서 ───────────────────────────────────────────────
    void ApplyVolume(string param, float linear01)
    {
        if (mixer == null) return; // 믹서 미할당 시 무시(점진 적용)
        // 0 → 무음(-80dB), 그 외 20*log10(v)
        float dB = linear01 <= 0.0001f ? -80f : Mathf.Log10(linear01) * 20f;
        mixer.SetFloat(param, dB);
    }

    // ── 사운드 로드 ───────────────────────────────────────────────
    void LoadAllSounds()
    {
        foreach (var clip in ResourceManager.Instance.GetAll<AudioClip>())
        {
            if (clip != null) _clips[clip.name] = clip;
        }

        if (_clips.Count == 0)
        {
            Debug.LogWarning("[AudioManager] 로드된 AudioClip이 없습니다. " +
                             "Resources 폴더에 오디오 클립이 있는지 확인하세요.");
            return;
        }

        Debug.Log($"[AudioManager] 사운드 {_clips.Count}개 로드: {string.Join(", ", _clips.Keys)}");
    }

    // ── 씬 이벤트 ─────────────────────────────────────────────────
    public void OnSceneLoadStart(string sceneName) => DisableAllChannels();
    public void OnSceneLoadComplete(string sceneName) { }

    void DisableAllChannels()
    {
        foreach (var kv in _active)
        {
            var st = kv.Value;
            if (st.source == null) continue;
            st.source.Stop();
            st.source.transform.SetParent(_poolParent, false);
            st.source.gameObject.SetActive(false);
        }
        _active.Clear();
    }

#if UNITY_EDITOR
    // 인스펙터 슬라이더를 플레이 중 조정하면 즉시 믹서에 반영
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplyVolume(MasterParam, masterVolume);
        ApplyVolume(SfxParam, sfxVolume);
        ApplyVolume(BgmParam, bgmVolume);
    }
#endif
}
