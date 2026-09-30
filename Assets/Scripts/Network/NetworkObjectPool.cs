using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Netcode for GameObjects (NGO) 환경에서 런타임 Instantiate/Destroy로 인한
/// GC 스파이크와 패킷 폭증을 방지하기 위한 공식 INetworkPrefabInstanceHandler 기반 네트워크 오브젝트 풀 매니저입니다.
/// </summary>
public class NetworkObjectPool : MonoBehaviour
{
    public static NetworkObjectPool Instance { get; private set; }

    [Serializable]
    public struct PoolConfig
    {
        public GameObject Prefab;
        public int PrewarmCount;
    }

    [Header("Pre-warm Configurations")]
    [SerializeField] private List<PoolConfig> _pooledPrefabs = new List<PoolConfig>();

    private readonly Dictionary<GameObject, Queue<NetworkObject>> _poolQueues = new Dictionary<GameObject, Queue<NetworkObject>>();
    private readonly Dictionary<NetworkObject, GameObject> _spawnedObjectToPrefabMap = new Dictionary<NetworkObject, GameObject>();
    private readonly HashSet<GameObject> _registeredPrefabs = new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializePools();
    }

    private void OnDestroy()
    {
        ClearPools();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 인스펙터에 등록된 풀 프리팹들을 초기화하고 NGO PrefabHandler에 등록합니다.
    /// </summary>
    public void InitializePools()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        foreach (var config in _pooledPrefabs)
        {
            if (config.Prefab != null)
            {
                RegisterPrefab(config.Prefab, config.PrewarmCount);
            }
        }
    }

    /// <summary>
    /// 특정 프리팹을 네트워크 풀에 등록하고 사전 생성(Pre-warm)합니다.
    /// </summary>
    public void RegisterPrefab(GameObject prefab, int prewarmCount)
    {
        if (prefab == null)
        {
            return;
        }

        var netObj = prefab.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[NetworkObjectPool] '{prefab.name}' 프리팹에 NetworkObject 컴포넌트가 없습니다.");
            return;
        }

        if (_registeredPrefabs.Contains(prefab))
        {
            return;
        }

        _registeredPrefabs.Add(prefab);

        if (!_poolQueues.ContainsKey(prefab))
        {
            _poolQueues[prefab] = new Queue<NetworkObject>();
        }

        // NGO의 PrefabHandler에 커스텀 풀 핸들러 등록
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.PrefabHandler != null)
        {
            NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new PooledPrefabInstanceHandler(prefab, this));
        }

        // 사전 생성 (Pre-warm)
        for (int i = 0; i < prewarmCount; i++)
        {
            CreateNewInstance(prefab);
        }
    }

    private NetworkObject CreateNewInstance(GameObject prefab)
    {
        GameObject go = Instantiate(prefab, transform);
        go.name = $"{prefab.name}_Pooled";
        go.SetActive(false);

        var netObj = go.GetComponent<NetworkObject>();
        _poolQueues[prefab].Enqueue(netObj);
        return netObj;
    }

    /// <summary>
    /// 풀에서 NetworkObject를 꺼내 활성화합니다 (서버 전용 호출).
    /// </summary>
    public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        if (!_poolQueues.ContainsKey(prefab))
        {
            RegisterPrefab(prefab, 10);
        }

        Queue<NetworkObject> queue = _poolQueues[prefab];
        NetworkObject netObj = null;

        while (queue.Count > 0)
        {
            netObj = queue.Dequeue();
            if (netObj != null)
            {
                break;
            }
        }

        if (netObj == null)
        {
            netObj = CreateNewInstance(prefab);
            queue.Dequeue(); // Enqueue된 것을 다시 Pop
        }

        GameObject go = netObj.gameObject;
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.SetParent(null);
        go.SetActive(true);

        _spawnedObjectToPrefabMap[netObj] = prefab;
        return netObj;
    }

    /// <summary>
    /// 스폰된 오브젝트를 풀에 반환합니다.
    /// </summary>
    public void ReturnNetworkObject(NetworkObject networkObject)
    {
        if (networkObject == null)
        {
            return;
        }

        if (!_spawnedObjectToPrefabMap.TryGetValue(networkObject, out GameObject prefab))
        {
            // 등록되지 않은 오브젝트인 경우 파괴
            Destroy(networkObject.gameObject);
            return;
        }

        _spawnedObjectToPrefabMap.Remove(networkObject);

        // 네트워크 디스폰이 아직 안 되었다면 실행 (호스트/서버인 경우)
        if (networkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            networkObject.Despawn(false);
        }

        GameObject go = networkObject.gameObject;
        go.SetActive(false);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        _poolQueues[prefab].Enqueue(networkObject);
    }

    private void ClearPools()
    {
        foreach (var kvp in _poolQueues)
        {
            Queue<NetworkObject> queue = kvp.Value;
            while (queue.Count > 0)
            {
                NetworkObject obj = queue.Dequeue();
                if (obj != null)
                {
                    Destroy(obj.gameObject);
                }
            }
        }

        _poolQueues.Clear();
        _spawnedObjectToPrefabMap.Clear();
        _registeredPrefabs.Clear();
    }

    /// <summary>
    /// NGO INetworkPrefabInstanceHandler 구현체:
    /// 클라이언트가 서버로부터 해당 프리팹 스폰 메시지를 수신했을 때 풀에서 인스턴스를 공급합니다.
    /// </summary>
    private class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _prefab;
        private readonly NetworkObjectPool _pool;

        public PooledPrefabInstanceHandler(GameObject prefab, NetworkObjectPool pool)
        {
            _prefab = prefab;
            _pool = pool;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return _pool.GetNetworkObject(_prefab, position, rotation);
        }

        public void Destroy(NetworkObject networkObject)
        {
            _pool.ReturnNetworkObject(networkObject);
        }
    }
}
