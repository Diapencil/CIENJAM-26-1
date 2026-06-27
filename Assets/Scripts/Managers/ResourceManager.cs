// ResourceManager.cs
// 기능 : 게임 시작 시 Resources 폴더 내 모든 에셋을 일괄 로드하여 "런타임 타입별 Dictionary"에 캐싱하는 싱글톤 매니저.
//        Addressable을 사용하지 않고 Resources 기반으로 모든 데이터(SO/프리팹/오디오/텍스처 등)에 일괄 접근하기 위한 진입점.
// 사용법 :
//   - 별도 배치 불필요. 최초 접근 시 자동으로 GameObject가 생성되어 DontDestroyOnLoad로 유지된다.
//     (씬에 직접 컴포넌트를 붙여도 동작한다.)
//   - 단일 조회   : ResourceManager.Instance.Get<SoundData>("Explosion");
//   - 전체 조회   : ResourceManager.Instance.GetAll<SoundData>();        // IEnumerable<SoundData>
//   - 존재 확인   : ResourceManager.Instance.TryGet<SoundData>("Explosion", out var data);
//   - 강제 재로드 : ResourceManager.Instance.Reload();
//
// 동작 정책 :
//   - Resources 전체를 무조건 일괄 로드(A안).
//   - 같은 "런타임 타입 + 같은 이름" 충돌 시 경고 로그 후 예외(InvalidOperationException)를 던진다.
//   - Get<T> 는 정확 타입뿐 아니라 T로 대입 가능한(상속/인터페이스) 타입까지 조회한다.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public class ResourceManager : MonoBehaviour
{
    private static ResourceManager _instance;

    public static ResourceManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<ResourceManager>();
            if (_instance != null)
                return _instance;

            var go = new GameObject(nameof(ResourceManager));
            _instance = go.AddComponent<ResourceManager>();
            return _instance;
        }
    }

    // 런타임 타입 -> (에셋 이름 -> 에셋) 2단 캐시
    private readonly Dictionary<Type, Dictionary<string, Object>> _cache = new();
    private bool _loaded;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureLoaded();
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        LoadAll();
        _loaded = true;
    }

    // Resources 폴더 전체를 일괄 로드하여 런타임 타입별로 버킷에 저장한다.
    private void LoadAll()
    {
        _cache.Clear();

        // 경로를 빈 문자열로 주면 Resources 하위 전체를 재귀적으로 로드한다.
        Object[] assets = Resources.LoadAll<Object>(string.Empty);

        int count = 0;
        foreach (Object asset in assets)
        {
            if (asset == null)
                continue;

            Type type = asset.GetType();
            if (!_cache.TryGetValue(type, out Dictionary<string, Object> bucket))
            {
                bucket = new Dictionary<string, Object>();
                _cache[type] = bucket;
            }

            if (bucket.ContainsKey(asset.name))
            {
                Debug.LogError($"[ResourceManager] 키 충돌: 타입 '{type.Name}' 에서 이름 '{asset.name}' 이(가) 중복됩니다. " +
                               $"Resources 내 같은 타입의 동일 이름 에셋을 제거하거나 이름을 변경하세요.");
                throw new InvalidOperationException(
                    $"[ResourceManager] Duplicate resource key: ({type.FullName}, \"{asset.name}\")");
            }

            bucket.Add(asset.name, asset);
            count++;
        }

        Debug.Log($"[ResourceManager] 로드 완료 : {count}개 에셋 / {_cache.Count}개 타입");
    }

    // 캐시를 비우고 Resources 전체를 다시 로드한다.
    public void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    // 이름으로 단일 에셋을 조회한다. 없으면 null.
    public T Get<T>(string name) where T : Object
    {
        EnsureLoaded();

        Type requested = typeof(T);

        // 1) 정확 타입 버킷 우선 조회
        if (_cache.TryGetValue(requested, out Dictionary<string, Object> exact)
            && exact.TryGetValue(name, out Object hit))
        {
            return (T)hit;
        }

        // 2) T로 대입 가능한(파생/구현) 타입 버킷까지 조회
        foreach (KeyValuePair<Type, Dictionary<string, Object>> pair in _cache)
        {
            if (pair.Key == requested || !requested.IsAssignableFrom(pair.Key))
                continue;

            if (pair.Value.TryGetValue(name, out Object asset))
                return (T)asset;
        }

        Debug.LogWarning($"[ResourceManager] 에셋을 찾을 수 없습니다 : 타입 '{requested.Name}', 이름 '{name}'");
        return null;
    }

    // 이름으로 단일 에셋을 조회한다. 성공 여부를 반환.
    public bool TryGet<T>(string name, out T result) where T : Object
    {
        EnsureLoaded();

        Type requested = typeof(T);

        if (_cache.TryGetValue(requested, out Dictionary<string, Object> exact)
            && exact.TryGetValue(name, out Object hit))
        {
            result = (T)hit;
            return true;
        }

        foreach (KeyValuePair<Type, Dictionary<string, Object>> pair in _cache)
        {
            if (pair.Key == requested || !requested.IsAssignableFrom(pair.Key))
                continue;

            if (pair.Value.TryGetValue(name, out Object asset))
            {
                result = (T)asset;
                return true;
            }
        }

        result = null;
        return false;
    }

    // 해당 타입(및 파생/구현 타입) 전체 에셋을 반환한다.
    public IEnumerable<T> GetAll<T>() where T : Object
    {
        EnsureLoaded();

        Type requested = typeof(T);
        var results = new List<T>();

        foreach (KeyValuePair<Type, Dictionary<string, Object>> pair in _cache)
        {
            if (!requested.IsAssignableFrom(pair.Key))
                continue;

            results.AddRange(pair.Value.Values.Cast<T>());
        }

        return results;
    }
}
