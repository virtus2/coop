using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coop.VFX
{
    /// <summary>
    /// 게임 내 모든 전투 및 환경 VFX 파티클을 중앙 집중식으로 풀링 관리하는 싱글톤 매니저입니다.
    /// 1. 표면 재질(SurfaceType)별 피격 파티클 풀링 및 프리팹 오버라이드 지원
    /// 2. 한도 초과 시 가장 오래된 활성 이펙트를 회수하는 FIFO(Steal Oldest) 정책
    /// 3. OnParticleSystemStopped 기반 무가비지(GC Free) 자동 반환
    /// 4. 프리팹 미할당 표면을 위한 런타임 캐싱 Fallback 템플릿 제공
    /// </summary>
    public class VfxPoolManager : MonoBehaviour
    {
        private static VfxPoolManager _instance;

        private bool _isInitialized = false;

        public static VfxPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<VfxPoolManager>();
                    if (_instance == null)
                    {
                        GameObject managerGo = new GameObject("[VfxPoolManager]");
                        _instance = managerGo.AddComponent<VfxPoolManager>();
                    }
                }
                _instance.EnsureInitialized();
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Pool Capacities")]
        [Tooltip("풀 초기 생성 시 각 VFX별로 미리 생성해둘 인스턴스 수입니다.")]
        [SerializeField] private int _initialPoolCapacity = 16;

        [Tooltip("각 VFX 풀별 최대 보관 가능 수입니다. 초과 요청 시 가장 오래된 인스턴스를 재활용(FIFO)합니다.")]
        [SerializeField] private int _maxPoolCapacity = 32;

        [Header("Surface Impact Prefabs (Optional)")]
        [SerializeField] private GameObject _defaultImpactPrefab;
        [SerializeField] private GameObject _stoneImpactPrefab;
        [SerializeField] private GameObject _fleshImpactPrefab;
        [SerializeField] private GameObject _metalImpactPrefab;
        [SerializeField] private GameObject _woodImpactPrefab;

        // 개별 풀 관리 클래스
        private class Pool
        {
            public GameObject Template;
            public Transform Container;
            public Queue<PooledVfxInstance> Available = new Queue<PooledVfxInstance>();
            public LinkedList<PooledVfxInstance> Active = new LinkedList<PooledVfxInstance>();
            public int MaxCapacity;
        }

        // Prefab 인스턴스 ID 또는 고유 키 기반 풀 딕셔너리
        private readonly Dictionary<int, Pool> _poolsByPrefabId = new Dictionary<int, Pool>();

        // SurfaceType별 템플릿 캐싱
        private readonly Dictionary<SurfaceType, GameObject> _surfaceTemplates = new Dictionary<SurfaceType, GameObject>();

        private Transform _poolRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
                return;
            }

            _instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            InitializePoolRoot();
            InitializeSurfaceTemplates();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void InitializePoolRoot()
        {
            if (_poolRoot == null)
            {
                GameObject rootGo = new GameObject("[VFX_Pool_Container]");
                rootGo.transform.SetParent(transform);
                _poolRoot = rootGo.transform;
            }
        }

        private void InitializeSurfaceTemplates()
        {
            // 각 SurfaceType별 지정된 프리팹 등록, 없으면 Fallback 동적 템플릿을 단 1회 생성하여 캐싱
            RegisterSurfaceTemplate(SurfaceType.Default, _defaultImpactPrefab, () => CreateFallbackParticle("Fallback_Default", new Color(0.75f, 0.7f, 0.6f, 0.8f), 1.8f, 0.12f, 8));
            RegisterSurfaceTemplate(SurfaceType.Stone, _stoneImpactPrefab, () => CreateFallbackParticle("Fallback_Stone", new Color(0.75f, 0.7f, 0.6f, 0.8f), 1.8f, 0.12f, 8));
            RegisterSurfaceTemplate(SurfaceType.Flesh, _fleshImpactPrefab, () => CreateFallbackParticle("Fallback_Flesh", new Color(0.65f, 0.05f, 0.05f, 0.9f), 2.2f, 0.10f, 12));
            RegisterSurfaceTemplate(SurfaceType.Metal, _metalImpactPrefab, () => CreateFallbackParticle("Fallback_Metal", new Color(1.0f, 0.85f, 0.3f, 1.0f), 3.5f, 0.06f, 10));
            RegisterSurfaceTemplate(SurfaceType.Wood, _woodImpactPrefab, () => CreateFallbackParticle("Fallback_Wood", new Color(0.55f, 0.35f, 0.15f, 0.9f), 2.0f, 0.09f, 8));
        }

        private void RegisterSurfaceTemplate(SurfaceType type, GameObject customPrefab, Func<GameObject> fallbackFactory)
        {
            GameObject template = customPrefab != null ? customPrefab : fallbackFactory();
            _surfaceTemplates[type] = template;

            // 시작 시 풀 Pre-warm 생성
            GetOrCreatePool(template);
        }

        private GameObject CreateFallbackParticle(string name, Color particleColor, float speed, float size, short burstCount)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_poolRoot);
            go.SetActive(false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = particleColor;
            main.stopAction = ParticleSystemStopAction.Callback;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, burstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;

            go.AddComponent<PooledVfxInstance>();
            return go;
        }

        private Pool GetOrCreatePool(GameObject template)
        {
            if (template == null) return null;

            int key = template.GetInstanceID();
            if (_poolsByPrefabId.TryGetValue(key, out Pool pool))
            {
                return pool;
            }

            pool = new Pool
            {
                Template = template,
                MaxCapacity = _maxPoolCapacity
            };

            GameObject containerGo = new GameObject($"Pool_{template.name}");
            containerGo.transform.SetParent(_poolRoot);
            pool.Container = containerGo.transform;

            _poolsByPrefabId[key] = pool;

            // 초기 용량 사전 인스턴스화 (Pre-warm)
            for (int i = 0; i < _initialPoolCapacity; i++)
            {
                PooledVfxInstance instance = CreateNewInstance(pool);
                pool.Available.Enqueue(instance);
            }

            return pool;
        }

        private PooledVfxInstance CreateNewInstance(Pool pool)
        {
            // 템플릿 프리팹의 playOnAwake로 인해 생성 순간 파티클이 터지는 것을 원천 차단하기 위해
            // 부모 컨테이너를 비활성화한 상태에서 Instantiate를 수행합니다.
            bool wasContainerActive = pool.Container.gameObject.activeSelf;
            if (wasContainerActive)
            {
                pool.Container.gameObject.SetActive(false);
            }

            GameObject go = Instantiate(pool.Template, pool.Container);
            go.name = $"{pool.Template.name}_Instance";

            // 모든 파티클 시스템의 playOnAwake를 끄고 잔여 파티클 즉시 소거
            ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                var main = particleSystems[i].main;
                main.playOnAwake = false;
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystems[i].Clear(true);
            }

            // 인스턴스 자체를 비활성화하여 보관
            go.SetActive(false);

            // 컨테이너 상태 복원
            if (wasContainerActive)
            {
                pool.Container.gameObject.SetActive(true);
            }

            PooledVfxInstance instance = go.GetComponent<PooledVfxInstance>();
            if (instance == null)
            {
                instance = go.AddComponent<PooledVfxInstance>();
            }

            instance.Initialize(returned => ReturnToPool(pool, returned));
            return instance;
        }

        private void ReturnToPool(Pool pool, PooledVfxInstance instance)
        {
            if (pool == null || instance == null) return;

            // 활성 목록에서 제거
            pool.Active.Remove(instance);

            // 가용 큐에 반환
            if (!pool.Available.Contains(instance))
            {
                pool.Available.Enqueue(instance);
            }
        }

        /// <summary>
        /// 표면 타입(SurfaceType)에 맞는 피격 파티클을 풀에서 가져와 스폰합니다.
        /// overridePrefab이 제공되면 해당 프리팹을 우선 풀링하여 사용합니다.
        /// </summary>
        public PooledVfxInstance SpawnImpact(SurfaceType surfaceType, Vector3 point, Vector3 normal, GameObject overridePrefab = null)
        {
            GameObject template = overridePrefab;
            if (template == null)
            {
                if (!_surfaceTemplates.TryGetValue(surfaceType, out template) || template == null)
                {
                    template = _surfaceTemplates[SurfaceType.Default];
                }
            }

            Quaternion rot = normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            Vector3 spawnPos = point + normal * 0.05f;

            return Spawn(template, spawnPos, rot);
        }

        /// <summary>
        /// 임의의 파티클 프리팹을 풀에서 꺼내어 주어진 위치와 회전값으로 재생합니다.
        /// 풀이 가득 찼을 경우 가장 오래된 활성 이펙트를 회수하여 재활용(FIFO)합니다.
        /// </summary>
        public PooledVfxInstance Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            Pool pool = GetOrCreatePool(prefab);
            if (pool == null) return null;

            PooledVfxInstance instance = null;

            // 1. 사용 가능한 인스턴스가 있는 경우
            if (pool.Available.Count > 0)
            {
                instance = pool.Available.Dequeue();
            }
            // 2. 가용 인스턴스가 없고 최대 한도에 도달한 경우 -> 가장 오래된 활성 인스턴스 강제 회수(FIFO Steal Oldest)
            else if (pool.Active.Count >= pool.MaxCapacity)
            {
                var oldestNode = pool.Active.First;
                if (oldestNode != null)
                {
                    instance = oldestNode.Value;
                    pool.Active.RemoveFirst();
                }
            }
            // 3. 아직 한도에 도달하지 않았으면 신규 생성
            if (instance == null)
            {
                instance = CreateNewInstance(pool);
            }

            // 활성 목록에 추가 (가장 최신)
            pool.Active.AddLast(instance);

            // 위치 설정 및 재생
            instance.ResetAndPlay(position, rotation);
            return instance;
        }
    }
}
