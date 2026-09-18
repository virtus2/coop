using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어가 E키로 상호작용하여 손에 들 수 있고,
/// 손에 든 상태에서 바닥을 클릭하여 다시 내려놓을 수 있는 상호작용 오브젝트입니다.
/// 로컬 단독 실행 및 Netcode(NGO) 환경 모두를 지원합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickableItem : NetworkBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] private string _promptText = "들기";
    [SerializeField] private Vector3 _holdOffset = Vector3.zero;
    [SerializeField] private Vector3 _holdRotation = Vector3.zero;
    [SerializeField] private float _dropVerticalOffset = 0.2f;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool _isHeld;
    private PlayerInteraction _currentHolder;

    public bool IsHeld => _isHeld;
    public float DropVerticalOffset => _dropVerticalOffset;

    // 네트워크 동기화용 변수: 들고 있는 플레이어의 NetworkObjectId (없으면 ulong.MaxValue)
    private readonly NetworkVariable<ulong> _holderNetworkObjectId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();

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

    public override void OnNetworkSpawn()
    {
        _holderNetworkObjectId.OnValueChanged += HandleHolderChanged;

        if (_holderNetworkObjectId.Value != ulong.MaxValue)
        {
            AttachToHolderById(_holderNetworkObjectId.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _holderNetworkObjectId.OnValueChanged -= HandleHolderChanged;
    }

    private void HandleHolderChanged(ulong previousValue, ulong newValue)
    {
        if (newValue != ulong.MaxValue)
        {
            AttachToHolderById(newValue);
        }
        else
        {
            DetachFromHolder();
        }
    }

    private void AttachToHolderById(ulong holderNetId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(holderNetId, out NetworkObject holderNetObj))
        {
            var interactor = holderNetObj.GetComponent<PlayerInteraction>();
            if (interactor != null)
            {
                InternalPickup(interactor);
            }
        }
    }

    private void DetachFromHolder()
    {
        InternalDrop(transform.position, transform.rotation);
    }

    #region IInteractable Implementation

    public bool CanInteract(PlayerInteraction interactor)
    {
        if (_isHeld)
        {
            return false;
        }

        if (interactor != null && interactor.IsHoldingItem)
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

        Pickup(interactor);
    }

    #endregion

    /// <summary>
    /// 플레이어가 이 물체를 집어 들도록 처리합니다.
    /// </summary>
    public void Pickup(PlayerInteraction interactor)
    {
        if (_isHeld || interactor == null)
        {
            return;
        }

        if (IsSpawned)
        {
            // 네트워크 환경: 서버에 들기 요청
            PickupServerRpc(interactor.NetworkObjectId);
        }
        else
        {
            // 로컬/싱글플레이 환경
            InternalPickup(interactor);
            interactor.OnItemPickedUp(this);
        }
    }

    /// <summary>
    /// 플레이어가 손에 든 물체를 지정된 위치와 회전으로 내려놓습니다.
    /// </summary>
    public void Drop(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (!_isHeld)
        {
            return;
        }

        if (IsSpawned)
        {
            // 네트워크 환경: 서버에 내려놓기 요청
            DropServerRpc(targetPosition, targetRotation);
        }
        else
        {
            // 로컬/싱글플레이 환경
            var previousHolder = _currentHolder;
            InternalDrop(targetPosition, targetRotation);
            if (previousHolder != null)
            {
                previousHolder.OnItemDropped(this);
            }
        }
    }

    private void LateUpdate()
    {
        if (_isHeld && _currentHolder != null)
        {
            Transform holdTarget = _currentHolder.HoldPoint != null ? _currentHolder.HoldPoint : _currentHolder.transform;
            Vector3 targetPos = holdTarget.TransformPoint(_holdOffset);
            Quaternion targetRot = holdTarget.rotation * Quaternion.Euler(_holdRotation);
            transform.SetPositionAndRotation(targetPos, targetRot);
        }
    }

    private void InternalPickup(PlayerInteraction interactor)
    {
        _isHeld = true;
        _currentHolder = interactor;

        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (_colliders == null)
        {
            _colliders = GetComponentsInChildren<Collider>();
        }

        // 충돌체 비활성화 (플레이어 충돌 및 시선 가림 방지)
        if (_colliders != null)
        {
            foreach (var col in _colliders)
            {
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }

        // NetworkObject의 계층(Parenting) 규칙 위반을 방지하기 위해 SetParent 대신
        // LateUpdate에서 홀더의 HoldPoint 위치/회전을 직접 추종(Follow)합니다.
        Transform holdTarget = interactor.HoldPoint != null ? interactor.HoldPoint : interactor.transform;
        Vector3 initialPos = holdTarget.TransformPoint(_holdOffset);
        Quaternion initialRot = holdTarget.rotation * Quaternion.Euler(_holdRotation);
        transform.SetPositionAndRotation(initialPos, initialRot);
    }

    private void InternalDrop(Vector3 targetPosition, Quaternion targetRotation)
    {
        _isHeld = false;
        transform.position = targetPosition;
        transform.rotation = targetRotation;

        if (_colliders == null)
        {
            _colliders = GetComponentsInChildren<Collider>();
        }

        // 충돌체 재활성화
        if (_colliders != null)
        {
            foreach (var col in _colliders)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }

        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        _currentHolder = null;
    }

    #region ServerRpc

    [ServerRpc(RequireOwnership = false)]
    private void PickupServerRpc(ulong interactorNetId)
    {
        _holderNetworkObjectId.Value = interactorNetId;

        // 소유자 클라이언트에게 아이템 획득 통지
        NotifyPickupClientRpc(interactorNetId);
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(ulong interactorNetId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(interactorNetId, out NetworkObject holderNetObj))
        {
            var interactor = holderNetObj.GetComponent<PlayerInteraction>();
            if (interactor != null && interactor.IsOwner)
            {
                interactor.OnItemPickedUp(this);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DropServerRpc(Vector3 targetPosition, Quaternion targetRotation)
    {
        _holderNetworkObjectId.Value = ulong.MaxValue;

        // 모든 클라이언트에 최종 내려놓기 위치/각도 동기화
        SyncDropPositionClientRpc(targetPosition, targetRotation);
    }

    [ClientRpc]
    private void SyncDropPositionClientRpc(Vector3 targetPosition, Quaternion targetRotation)
    {
        InternalDrop(targetPosition, targetRotation);

        if (_currentHolder != null && _currentHolder.IsOwner)
        {
            _currentHolder.OnItemDropped(this);
        }
    }

    #endregion
}
