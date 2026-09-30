using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 월드에 스폰되어 물리 시뮬레이션되는 상호작용 아이템 객체입니다.
/// 단일 프리팹 풀링(GenericWorldItem)을 지원하여 NetworkVariable로 동기화된 ItemId에 맞춰
/// MeshFilter, MeshRenderer, Convex MeshCollider를 동적으로 교체합니다.
/// 바닥에 안착하면 Kinematic으로 고정(Sleep & Freeze)되어 물리 연산과 대역폭을 최적화하며,
/// 플레이어 접촉이나 사격/타격 시 깨어나 물리 반응을 수행합니다.
/// </summary>
public class PickableItem : NetworkBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] private string _promptText = "들기";
    [SerializeField] private float _dropVerticalOffset = 0.2f;

    [Header("Inventory Settings")]
    [SerializeField] private ItemData _itemData;

    [Header("Dynamic Visual & Physics")]
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private MeshCollider _meshCollider;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool _isDespawning;

    // 단일 프리팹 풀링을 위한 네트워크 아이템 ID 동기화 변수
    private readonly NetworkVariable<FixedString64Bytes> _networkItemId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float DropVerticalOffset => _dropVerticalOffset;

    public ItemData ItemData
    {
        get => _itemData;
        set
        {
            _itemData = value;
            if (_itemData != null)
            {
                if (IsServer && IsSpawned)
                {
                    _networkItemId.Value = _itemData.ItemId;
                }
                ApplyItemDataVisualAndPhysics(_itemData);
            }
        }
    }

    public string NetworkItemId => _networkItemId.Value.ToString();
    public Rigidbody Rigidbody => _rigidbody;

    // 물리 안정화 및 수면(Sleep & Freeze) 최적화 변수
    private bool _isSettled;
    private float _stillTimer;
    private const float SETTLE_THRESHOLD = 0.15f;
    private const float SETTLE_SPEED_SQR = 0.005f;

    public bool IsSettled => _isSettled;
    private Renderer[] _highlightRenderers;

    // 하위 호환성 빈 메서드 및 프로퍼티
    public bool IsHeld => false;
    public void SetThrower(ulong throwerNetId) { }
    public void IgnoreCollisionWithThrower(GameObject thrower) { }
    public void RestoreThrowerCollision() { }

    private void Awake()
    {
        EnsureComponents();

        int pickableLayer = LayerMask.NameToLayer("PickableItem");
        if (pickableLayer >= 0)
        {
            gameObject.layer = pickableLayer;
            var children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                child.gameObject.layer = pickableLayer;
            }
        }

        UpdateDropOffset();
    }

    private void EnsureComponents()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshCollider == null) _meshCollider = GetComponent<MeshCollider>();
        if (_colliders == null || _colliders.Length == 0) _colliders = GetComponentsInChildren<Collider>();
        if (_highlightRenderers == null || _highlightRenderers.Length == 0) _highlightRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void UpdateDropOffset()
    {
        if (_colliders != null && _colliders.Length > 0 && _colliders[0] != null)
        {
            float calculatedHalfY = _colliders[0].bounds.extents.y;
            if (calculatedHalfY > 0.01f)
            {
                _dropVerticalOffset = calculatedHalfY;
            }
        }
    }

    private void OnEnable()
    {
        _isDespawning = false;
        ItemCullingManager.Register(this);
    }

    private void OnDisable()
    {
        ItemCullingManager.Unregister(this);
    }

    public override void OnNetworkSpawn()
    {
        EnsureComponents();
        _networkItemId.OnValueChanged += HandleNetworkItemIdChanged;

        if (!string.IsNullOrEmpty(_networkItemId.Value.ToString()))
        {
            ApplyItemById(_networkItemId.Value.ToString());
        }
        else if (_itemData != null)
        {
            if (IsServer)
            {
                _networkItemId.Value = _itemData.ItemId;
            }
            ApplyItemDataVisualAndPhysics(_itemData);
        }
    }

    public override void OnNetworkDespawn()
    {
        _networkItemId.OnValueChanged -= HandleNetworkItemIdChanged;
        _isDespawning = true;
        base.OnNetworkDespawn();
    }

    private void HandleNetworkItemIdChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        ApplyItemById(newValue.ToString());
    }

    /// <summary>
    /// 동기화된 아이템 ID를 기반으로 ItemDatabase에서 데이터를 로드하고 메쉬 및 콜라이더를 갱신합니다.
    /// </summary>
    public void ApplyItemById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return;
        }

        ItemData data = ItemDatabase.GetItem(itemId);
        if (data != null)
        {
            _itemData = data;
            ApplyItemDataVisualAndPhysics(data);
        }
    }

    /// <summary>
    /// ItemData에 정의된 메쉬, 머티리얼, 스케일 및 Convex MeshCollider를 동적으로 적용합니다.
    /// </summary>
    public void ApplyItemDataVisualAndPhysics(ItemData data)
    {
        if (data == null)
        {
            return;
        }

        EnsureComponents();

        Mesh targetMesh = data.HeldMesh;
        Material targetMat = data.HeldMaterial;

        // Held visual이 없으면 WorldPrefab에서 메쉬 탐색
        if (targetMesh == null && data.WorldPrefab != null)
        {
            var mf = data.WorldPrefab.GetComponentInChildren<MeshFilter>(true);
            if (mf != null) targetMesh = mf.sharedMesh;
        }
        if (targetMat == null && data.WorldPrefab != null)
        {
            var mr = data.WorldPrefab.GetComponentInChildren<MeshRenderer>(true);
            if (mr != null) targetMat = mr.sharedMaterial;
        }

        if (targetMesh != null)
        {
            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = targetMesh;
            }

            if (_meshRenderer != null && targetMat != null)
            {
                _meshRenderer.sharedMaterial = targetMat;
            }

            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = null; // 리셋 후 재할당
                _meshCollider.sharedMesh = targetMesh;
                _meshCollider.convex = true;
            }

            transform.localScale = data.HeldLocalScale != Vector3.zero ? data.HeldLocalScale : Vector3.one;
            _colliders = GetComponentsInChildren<Collider>();
            _highlightRenderers = GetComponentsInChildren<Renderer>(true);
            UpdateDropOffset();
        }

        // 아이템 무게 설정 (기본값 5kg)
        if (_rigidbody != null)
        {
            _rigidbody.mass = 5.0f;
        }
    }

    #region IInteractable Implementation

    public Renderer[] GetHighlightRenderers()
    {
        if (_highlightRenderers == null || _highlightRenderers.Length == 0)
        {
            _highlightRenderers = GetComponentsInChildren<Renderer>(true);
        }
        return _highlightRenderers;
    }

    public bool CanInteract(PlayerInteraction interactor)
    {
        if (_isDespawning)
        {
            return false;
        }

        if (interactor == null)
        {
            return false;
        }

        if (interactor.ItemHolder != null && interactor.ItemHolder.IsHoldingWorldItem)
        {
            return false;
        }

        return true;
    }

    public string GetInteractionPrompt()
    {
        return _promptText;
    }

    public void Interact(PlayerInteraction interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        interactor.PickupWorldItem(this);
    }

    #endregion

    private void FixedUpdate()
    {
        if (_isSettled || _rigidbody == null || _rigidbody.isKinematic)
        {
            return;
        }

        if (IsServer || !IsSpawned)
        {
            bool isStill = _rigidbody.IsSleeping() ||
                           (_rigidbody.linearVelocity.sqrMagnitude < SETTLE_SPEED_SQR &&
                            _rigidbody.angularVelocity.sqrMagnitude < SETTLE_SPEED_SQR);

            if (isStill)
            {
                _stillTimer += Time.fixedDeltaTime;
                if (_stillTimer >= SETTLE_THRESHOLD)
                {
                    SettlePhysics();
                }
            }
            else
            {
                _stillTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 바닥에 안착했을 때 물리를 Kinematic으로 고정하여 PhysX 연산 부하를 0으로 만듭니다.
    /// </summary>
    public void SettlePhysics()
    {
        if (_isSettled || _rigidbody == null)
        {
            return;
        }

        _isSettled = true;
        _stillTimer = 0f;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;
    }

    /// <summary>
    /// 외부 충격이나 폭발 등으로 멈춰있던 아이템을 다시 물리 시뮬레이션 상태로 깨웁니다.
    /// </summary>
    public void WakeUpPhysics(Vector3 force = default)
    {
        _isSettled = false;
        _stillTimer = 0f;

        if (IsServer || !IsSpawned)
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                if (force != Vector3.zero)
                {
                    _rigidbody.AddForce(force, ForceMode.Impulse);
                }
            }
        }
    }

    /// <summary>
    /// 사격이나 근접 공격 등으로 외력을 가합니다.
    /// </summary>
    public void ApplyImpulse(Vector3 force, Vector3 hitPoint = default)
    {
        WakeUpPhysics();

        if (IsServer || !IsSpawned)
        {
            if (_rigidbody != null)
            {
                if (hitPoint != default)
                {
                    _rigidbody.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
                }
                else
                {
                    _rigidbody.AddForce(force, ForceMode.Impulse);
                }
            }
        }
        else
        {
            ApplyImpulseServerRpc(force, hitPoint);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyImpulseServerRpc(Vector3 force, Vector3 hitPoint)
    {
        ApplyImpulse(force, hitPoint);
    }

    public void SetInitialDropPhysics(Vector3 initialVelocity)
    {
        _isSettled = false;
        _stillTimer = 0f;

        EnsureComponents();

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = initialVelocity;
            _rigidbody.angularVelocity = Random.insideUnitSphere * 2f; // 자연스러운 공중 회전
        }
    }

    public void Drop(Vector3 targetPosition, Quaternion targetRotation, Vector3 initialVelocity = default, GameObject thrower = null)
    {
        transform.position = targetPosition;
        transform.rotation = targetRotation;
        SetInitialDropPhysics(initialVelocity);
    }
}
