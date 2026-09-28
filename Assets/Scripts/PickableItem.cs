using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 월드에 스폰되어 물리 시뮬레이션되는 상호작용 아이템 객체입니다.
/// NetworkTransform 및 NetworkRigidbody를 통해 멀티플레이 환경에서 완벽한 물리 동기화를 지원하며,
/// 바닥에 닿아 멈추면 Kinematic으로 고정(기법 1)되어 성능을 최적화합니다.
/// 플레이어가 E키로 상호작용하여 획득하면 인벤토리/손으로 들어가고 월드에서는 즉시 Despawn됩니다.
/// </summary>
public class PickableItem : NetworkBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] private string _promptText = "들기";
    [SerializeField] private float _dropVerticalOffset = 0.2f;

    [Header("Inventory Settings")]
    [SerializeField] private ItemData _itemData;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool _isDespawning;

    public float DropVerticalOffset => _dropVerticalOffset;

    public ItemData ItemData
    {
        get => _itemData;
        set => _itemData = value;
    }

    // 물리 안정화 및 수면(Sleep & Freeze) 최적화 변수 (기법 1)
    private bool _isSettled;
    private float _stillTimer;
    private const float SETTLE_THRESHOLD = 0.15f;
    private const float SETTLE_SPEED_SQR = 0.005f;

    public bool IsSettled => _isSettled;
    private Renderer[] _highlightRenderers;

    // 하위 호환성 빈 메서드 및 프로퍼티 (필요시 호출 방어)
    public bool IsHeld => false;
    public void SetThrower(ulong throwerNetId) { }
    public void IgnoreCollisionWithThrower(GameObject thrower) { }
    public void RestoreThrowerCollision() { }

    private void Awake()
    {
        // 1. PickableItem 전용 레이어 자동 할당 (Item vs Item 및 Item vs Player 충돌 무시 매트릭스 적용 - 기법 2)
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

        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _highlightRenderers = GetComponentsInChildren<Renderer>(true);

        if (_colliders.Length > 0)
        {
            // 콜라이더의 높이 절반을 기본 드롭 오프셋으로 자동 보정
            float calculatedHalfY = _colliders[0].bounds.extents.y;
            if (calculatedHalfY > 0.01f)
            {
                _dropVerticalOffset = calculatedHalfY;
            }
        }
    }

    private void OnEnable()
    {
        ItemCullingManager.Register(this);
    }

    private void OnDisable()
    {
        ItemCullingManager.Unregister(this);
    }

    public override void OnNetworkDespawn()
    {
        _isDespawning = true;
        base.OnNetworkDespawn();
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

        // 이미 바닥에서 주운 미수납 아이템을 손에 쥐고 있다면 추가 줍기 상호작용 비활성화
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

        // 플레이어에게 아이템 획득 처리 위임
        interactor.PickupWorldItem(this);
    }

    #endregion

    private void FixedUpdate()
    {
        // 1. 이미 안착(Kinematic)된 경우 연산 스킵 (기법 1)
        if (_isSettled || _rigidbody == null || _rigidbody.isKinematic)
        {
            return;
        }

        // 2. 서버 측 물리 수면 판정: IsSleeping이거나 속도가 임계값 이하로 지속될 때
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
    /// 아이템이 바닥에 안정적으로 안착했을 때 물리를 Kinematic으로 고정하여 PhysX 연산 부하 및 NetworkTransform 전송을 0으로 만듭니다. (기법 1)
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
    /// 아이템을 스폰 또는 드롭할 때 초기 위치, 회전, 투척 속도를 설정합니다.
    /// </summary>
    public void SetInitialDropPhysics(Vector3 initialVelocity)
    {
        _isSettled = false;
        _stillTimer = 0f;

        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = initialVelocity;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    // 하위 호환성 지원용
    public void InternalDrop(Vector3 targetPosition, Quaternion targetRotation) => Drop(targetPosition, targetRotation);

    public void Drop(Vector3 targetPosition, Quaternion targetRotation, Vector3 initialVelocity = default, GameObject thrower = null)
    {
        transform.position = targetPosition;
        transform.rotation = targetRotation;
        SetInitialDropPhysics(initialVelocity);
    }
}
