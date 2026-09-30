using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Coop.VFX
{
    /// <summary>
    /// 게임 내 총탄 구멍(Bullet Hole) 데칼을 중앙 집중식으로 관리하는 풀링 매니저입니다.
    /// 1. 최대 128개 제한 및 초과 시 FIFO(가장 오래된 데칼 강제 회수) 정책
    /// 2. 8초 활성 유지 후 1초간 페이드아웃(fadeFactor)
    /// 3. 표면 재질(SurfaceType)별 데칼 머티리얼 지원 (Flesh는 방안 A 정책에 따라 데칼 생성 배제)
    /// 4. 정적 지형 및 이동 객체(Transform 추적) 완벽 대응
    /// 5. 모서리 왜곡 방지(Z-depth 0.15m + Angle Fade 50~75도)
    /// </summary>
    public class DecalPoolManager : MonoBehaviour
    {
        private static DecalPoolManager _instance;

        public static DecalPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DecalPoolManager>();
                    if (_instance == null)
                    {
                        GameObject managerGo = new GameObject("[DecalPoolManager]");
                        _instance = managerGo.AddComponent<DecalPoolManager>();
                    }
                }
                _instance.EnsureInitialized();
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Pool Capacities")]
        [Tooltip("초기 사전 생성 데칼 개수")]
        [SerializeField] private int _initialPoolCapacity = 32;

        [Tooltip("최대 동시 유지 데칼 개수 (초과 시 FIFO로 가장 오래된 것 재사용)")]
        [SerializeField] private int _maxPoolCapacity = 128;

        [Header("Decal Timings")]
        [Tooltip("데칼 선명 유지 시간 (초)")]
        [SerializeField] private float _activeDuration = 8.0f;

        [Tooltip("페이드아웃 소요 시간 (초)")]
        [SerializeField] private float _fadeDuration = 1.0f;

        [Header("Decal Projection Settings")]
        [Tooltip("데칼 투영 박스 크기 (X=가로, Y=세로, Z=투영깊이)")]
        [SerializeField] private Vector3 _decalSize = new Vector3(0.14f, 0.14f, 0.2f);

        [Tooltip("모서리 각도 페이드 시작 각도 (도)")]
        [SerializeField] private float _startAngleFade = 50f;

        [Tooltip("모서리 각도 페이드 종료 각도 (도)")]
        [SerializeField] private float _endAngleFade = 75f;

        [Header("Decal Materials")]
        [Tooltip("기본 총탄 구멍 머티리얼 (M_DecalProjector_BulletHole)")]
        [SerializeField] private Material _defaultBulletHoleMaterial;

        [Tooltip("석재/콘크리트용 총탄 구멍 머티리얼 (미할당 시 기본 사용)")]
        [SerializeField] private Material _stoneBulletHoleMaterial;

        [Tooltip("금속용 총탄 구멍 머티리얼 (미할당 시 기본 사용)")]
        [SerializeField] private Material _metalBulletHoleMaterial;

        [Tooltip("목재용 총탄 구멍 머티리얼 (미할당 시 기본 사용)")]
        [SerializeField] private Material _woodBulletHoleMaterial;

        private readonly Queue<PooledDecalInstance> _availablePool = new Queue<PooledDecalInstance>();
        private readonly LinkedList<PooledDecalInstance> _activeList = new LinkedList<PooledDecalInstance>();

        private Transform _poolContainer;
        private bool _isInitialized = false;

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

            if (_poolContainer == null)
            {
                GameObject containerGo = new GameObject("[Decal_Pool_Container]");
                containerGo.transform.SetParent(transform);
                _poolContainer = containerGo.transform;
            }

            // 머티리얼이 에디터/인스펙터에서 미할당된 경우 기본 번들 에셋 로드 시도
            if (_defaultBulletHoleMaterial == null)
            {
#if UNITY_EDITOR
                _defaultBulletHoleMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/FreeSurfaceDecals5Pack/Materials/URP Projector/M_DecalProjector_BulletHole.mat");
#endif
            }

            // 초기 용량 사전 생성 (Pre-warm)
            for (int i = 0; i < _initialPoolCapacity; i++)
            {
                PooledDecalInstance instance = CreateNewDecalInstance();
                _availablePool.Enqueue(instance);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private PooledDecalInstance CreateNewDecalInstance()
        {
            GameObject go = new GameObject("Decal_BulletHole_Instance");
            go.transform.SetParent(_poolContainer);
            go.SetActive(false);

            DecalProjector projector = go.AddComponent<DecalProjector>();
            projector.size = _decalSize;
            projector.pivot = Vector3.zero;
            projector.startAngleFade = _startAngleFade;
            projector.endAngleFade = _endAngleFade;
            projector.fadeFactor = 1.0f;

            PooledDecalInstance instance = go.AddComponent<PooledDecalInstance>();
            instance.Initialize(OnDecalReturned);
            return instance;
        }

        private void OnDecalReturned(PooledDecalInstance instance)
        {
            if (instance == null) return;

            _activeList.Remove(instance);
            if (!_availablePool.Contains(instance))
            {
                _availablePool.Enqueue(instance);
            }
        }

        /// <summary>
        /// 탄착 지점에 총탄 구멍 데칼을 스폰합니다.
        /// Flesh 표면이나 캐릭터/몬스터 피격 시에는 기획 방안 A에 따라 데칼을 생성하지 않습니다.
        /// </summary>
        public void SpawnBulletHole(Vector3 point, Vector3 normal, SurfaceType surfaceType = SurfaceType.Default, Transform target = null)
        {
            // 1. 방안 A: 몬스터 / 캐릭터 / 생체 타겟(Flesh) 피격 시 데칼 미생성
            if (surfaceType == SurfaceType.Flesh)
            {
                return;
            }

            if (target != null)
            {
                // 플레이어, 몬스터, NPC, 래그돌 본인 경우 데칼 스폰 방지
                if (target.GetComponentInParent<PlayerController>() != null ||
                    target.GetComponentInParent<Hitbox>() != null ||
                    target.GetComponentInParent<NonPlayerCharacter>() != null)
                {
                    return;
                }
            }

            EnsureInitialized();

            PooledDecalInstance instance = null;

            // 2. 가용 인스턴스가 있는 경우
            if (_availablePool.Count > 0)
            {
                instance = _availablePool.Dequeue();
            }
            // 3. 풀이 가득 찬 경우 (128개 도달): 가장 오래된 데칼 강제 회수 (FIFO)
            else if (_activeList.Count >= _maxPoolCapacity)
            {
                var oldestNode = _activeList.First;
                if (oldestNode != null)
                {
                    instance = oldestNode.Value;
                    instance.ForceEvict();
                    _activeList.RemoveFirst();
                }
            }

            // 4. 아직 최대 용량 미도달 시 신규 인스턴스 생성
            if (instance == null)
            {
                instance = CreateNewDecalInstance();
            }

            _activeList.AddLast(instance);

            // 5. 표면 재질별 머티리얼 선택
            Material mat = GetMaterialForSurface(surfaceType);

            // 6. 위치 및 회전 연산
            // 법선(normal) 방향으로 프로젝터를 표면에서 살짝 후퇴/돌출시켜(depth의 25%)
            // 투영 박스가 충돌 표면의 앞뒤를 고르게 감싸도록 배치합니다 (바닥 속 묻힘/공중부양 클리핑 원천 방지).
            Vector3 spawnPos = point + normal * (_decalSize.z * 0.25f);

            Vector3 forward = -normal;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Quaternion lookRot = Quaternion.LookRotation(forward, up);
            float randomRoll = Random.Range(0f, 360f);
            Quaternion finalRot = lookRot * Quaternion.AngleAxis(randomRoll, Vector3.forward);

            // 7. 인스턴스 활성화 및 재생
            instance.Play(
                position: spawnPos,
                rotation: finalRot,
                decalMaterial: mat,
                size: _decalSize,
                startAngleFade: _startAngleFade,
                endAngleFade: _endAngleFade,
                activeDuration: _activeDuration,
                fadeDuration: _fadeDuration,
                target: target
            );
        }

        private Material GetMaterialForSurface(SurfaceType surfaceType)
        {
            switch (surfaceType)
            {
                case SurfaceType.Stone:
                    if (_stoneBulletHoleMaterial != null) return _stoneBulletHoleMaterial;
                    break;
                case SurfaceType.Metal:
                    if (_metalBulletHoleMaterial != null) return _metalBulletHoleMaterial;
                    break;
                case SurfaceType.Wood:
                    if (_woodBulletHoleMaterial != null) return _woodBulletHoleMaterial;
                    break;
            }

            return _defaultBulletHoleMaterial;
        }

        public void SetDefaultMaterial(Material mat)
        {
            _defaultBulletHoleMaterial = mat;
        }
    }
}
