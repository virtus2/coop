using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 월드에 드롭된 수백 개의 PickableItem을 대상으로 거리 기반 렌더링 및 그림자 컬링을 수행합니다. (기법 5)
/// - 15m 이상 떨어진 아이템: 실시간 그림자 캐스팅 비활성화 (GPU Shadow Pass 및 드로우 콜 대폭 절감)
/// - 50m 이상 떨어진 아이템: 메쉬 렌더러 비활성화 (원거리 렌더링 부하 완전 제거)
/// - 0.3초 주기로 sqrMagnitude 고속 연산을 사용하여 CPU 부하를 최소화합니다.
/// </summary>
public class ItemCullingManager : MonoBehaviour
{
    public static ItemCullingManager Instance { get; private set; }

    [Header("Culling Distances")]
    [SerializeField] private float _shadowCullDistance = 15f;
    [SerializeField] private float _renderCullDistance = 50f;
    [SerializeField] private float _checkInterval = 0.3f;

    private float _shadowSqr;
    private float _renderSqr;
    private float _timer;

    private static readonly List<ItemEntry> _items = new List<ItemEntry>();
    private Camera _mainCamera;

    private class ItemEntry
    {
        public PickableItem item;
        public Renderer[] renderers;
        public bool isShadowOn = true;
        public bool isRenderOn = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
        _items.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            var go = new GameObject("[ItemCullingManager]");
            Instance = go.AddComponent<ItemCullingManager>();
            DontDestroyOnLoad(go);
        }
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

        _shadowSqr = _shadowCullDistance * _shadowCullDistance;
        _renderSqr = _renderCullDistance * _renderCullDistance;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _checkInterval)
        {
            return;
        }

        _timer = 0f;
        UpdateCulling();
    }

    public static void Register(PickableItem item)
    {
        if (item == null) return;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].item == item) return;
        }

        var entry = new ItemEntry
        {
            item = item,
            renderers = item.GetComponentsInChildren<Renderer>(true),
            isShadowOn = true,
            isRenderOn = true
        };

        _items.Add(entry);
    }

    public static void Unregister(PickableItem item)
    {
        if (item == null) return;

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].item == item || _items[i].item == null)
            {
                _items.RemoveAt(i);
            }
        }
    }

    private void UpdateCulling()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 camPos = _mainCamera.transform.position;

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var entry = _items[i];
            if (entry.item == null)
            {
                _items.RemoveAt(i);
                continue;
            }

            // 손에 들려있는 아이템은 1인칭 모델이므로 컬링 제외
            if (entry.item.IsHeld)
            {
                SetRenderersVisible(entry, true, true);
                continue;
            }

            float distSqr = (entry.item.transform.position - camPos).sqrMagnitude;

            // 50m 초과: 렌더러 비활성화
            if (distSqr > _renderSqr)
            {
                SetRenderersVisible(entry, false, false);
            }
            // 15m ~ 50m: 메쉬는 보이되 그림자는 끔
            else if (distSqr > _shadowSqr)
            {
                SetRenderersVisible(entry, true, false);
            }
            // 15m 이내: 메쉬 및 그림자 모두 활성화
            else
            {
                SetRenderersVisible(entry, true, true);
            }
        }
    }

    private void SetRenderersVisible(ItemEntry entry, bool renderVisible, bool shadowVisible)
    {
        if (entry.renderers == null) return;

        if (entry.isRenderOn != renderVisible)
        {
            entry.isRenderOn = renderVisible;
            for (int j = 0; j < entry.renderers.Length; j++)
            {
                if (entry.renderers[j] != null)
                {
                    entry.renderers[j].enabled = renderVisible;
                }
            }
        }

        if (entry.isShadowOn != shadowVisible && renderVisible)
        {
            entry.isShadowOn = shadowVisible;
            ShadowCastingMode mode = shadowVisible ? ShadowCastingMode.On : ShadowCastingMode.Off;
            for (int j = 0; j < entry.renderers.Length; j++)
            {
                if (entry.renderers[j] != null)
                {
                    entry.renderers[j].shadowCastingMode = mode;
                }
            }
        }
    }
}
