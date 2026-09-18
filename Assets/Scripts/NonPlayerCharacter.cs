using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 월드 상에서 자율적으로 움직이거나 상호작용하는 몬스터 및 NPC의 베이스 클래스입니다.
///
/// [Authority vs Ownership 설계 원칙]
/// 1. Server-Authoritative:
///    - 모든 상태 결정, 이동, 피격 및 사망 판정은 오직 서버(IsServer)에서만 연산 및 갱신됩니다.
/// 2. Ownership의 명확한 분리:
///    - Unity Netcode for GameObjects(NGO)에서 호스트(Host, ClientId = 0) 환경일 때,
///      서버 스폰된 객체의 내부 OwnerClientId가 ServerClientId로 자동 지정되어
///      기술적으로 NGO의 IsOwner가 true를 반환할 수 있습니다.
///    - 하지만 이 캐릭터는 플레이어가 소유하거나 조종하는 캐릭터가 아닙니다.
///    - 따라서 HasPlayerOwner, IsPlayerControlled는 항상 false를 반환하며,
///      플레이어 전용 로직(PlayerController, 카메라 부착, 플레이어 입력, HUD 등)에 관여하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public abstract class NonPlayerCharacter : NetworkBehaviour
{
    [Header("Base Character Settings")]
    [SerializeField] private string _defaultName = "NonPlayerCharacter";
    [SerializeField] private int _maxHealth = 100;

    [Header("Despawn Settings")]
    [SerializeField] private bool _autoDespawnOnDeath = true;
    [SerializeField] private float _despawnDelay = 3f;

    // --- 소유권 및 권한 식별 프로퍼티 ---

    /// <summary>
    /// 실제 인간 플레이어가 이 캐릭터를 소유하고 있는지 여부입니다.
    /// 몬스터 및 NPC는 서버 엔티티이므로 항상 false를 반환합니다.
    /// </summary>
    public virtual bool HasPlayerOwner => false;

    /// <summary>
    /// 로컬 머신의 플레이어 입력에 의해 조종되는지 여부입니다.
    /// 몬스터 및 NPC는 AI 또는 서버 스크립트에 의해 제어되므로 항상 false입니다.
    /// </summary>
    public virtual bool IsPlayerControlled => false;

    /// <summary>
    /// 서버가 모든 로직과 상태를 전적으로 제어(Server-Authoritative)하는지 여부입니다.
    /// </summary>
    public bool IsServerAuthoritative => true;

    // --- 서버 권한 네트워크 동기화 변수 ---

    private readonly NetworkVariable<CharacterState> _currentState = new NetworkVariable<CharacterState>(
        CharacterState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> _currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> _characterName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- 상태 및 이벤트 ---

    public CharacterState CurrentState => _currentState.Value;
    public int CurrentHealth => _currentHealth.Value;
    public int MaxHealth => _maxHealth;
    public string CharacterName => _characterName.Value.ToString();
    public bool IsDead => _currentState.Value == CharacterState.Dead;

    public event Action<CharacterState, CharacterState> OnStateChanged;
    public event Action<int, int> OnHealthChanged;
    public event Action<int, ulong> OnDamaged;
    public event Action OnDied;

    protected Collider CharacterCollider;

    protected virtual void Awake()
    {
        CharacterCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        _currentState.OnValueChanged += HandleStateChanged;
        _currentHealth.OnValueChanged += HandleHealthChanged;

        if (IsServer)
        {
            _characterName.Value = new FixedString64Bytes(_defaultName);
            _currentHealth.Value = _maxHealth;
            _currentState.Value = CharacterState.Idle;
        }
    }

    public override void OnNetworkDespawn()
    {
        _currentState.OnValueChanged -= HandleStateChanged;
        _currentHealth.OnValueChanged -= HandleHealthChanged;
    }

    private void HandleStateChanged(CharacterState previousState, CharacterState newState)
    {
        OnStateChanged?.Invoke(previousState, newState);
        OnClientStateChanged(previousState, newState);
    }

    private void HandleHealthChanged(int previousHealth, int newHealth)
    {
        OnHealthChanged?.Invoke(previousHealth, newHealth);
    }

    /// <summary>
    /// 클라이언트 측에서 상태 변경 시 비주얼(애니메이션, 이펙트 등)을 반영하기 위한 가상 메서드입니다.
    /// </summary>
    protected virtual void OnClientStateChanged(CharacterState previousState, CharacterState newState)
    {
        // 파생 클래스에서 애니메이션 및 비주얼 처리
    }

    /// <summary>
    /// 서버에서만 실행되는 상태 변경 메서드입니다.
    /// </summary>
    /// <param name="newState">변경할 새로운 상태</param>
    protected void SetState(CharacterState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[NonPlayerCharacter] 상태 변경은 오직 서버에서만 가능합니다! (오브젝트: {name})");
            return;
        }

        if (_currentState.Value == CharacterState.Dead)
        {
            return; // 이미 사망한 경우 상태 변경 무시
        }

        _currentState.Value = newState;
    }

    /// <summary>
    /// 데미지를 입히는 메서드입니다. 서버 권한(Server-Authoritative)으로 동작합니다.
    /// </summary>
    /// <param name="damage">입힐 데미지 양</param>
    /// <param name="instigatorClientId">데미지를 가한 주체의 ClientId</param>
    public virtual void TakeDamage(int damage, ulong instigatorClientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[NonPlayerCharacter] TakeDamage는 서버에서만 호출되어야 합니다! (호출자: ClientId={instigatorClientId})");
            return;
        }

        if (IsDead || damage <= 0)
        {
            return;
        }

        int previousHealth = _currentHealth.Value;
        int newHealth = Mathf.Max(0, previousHealth - damage);
        _currentHealth.Value = newHealth;

        NotifyDamagedClientRpc(damage, instigatorClientId);

        if (newHealth <= 0)
        {
            HandleDeath();
        }
    }

    [ClientRpc]
    private void NotifyDamagedClientRpc(int damage, ulong instigatorClientId)
    {
        OnDamaged?.Invoke(damage, instigatorClientId);
    }

    /// <summary>
    /// 사망 처리 로직입니다. 서버에서 호출됩니다.
    /// </summary>
    protected virtual void HandleDeath()
    {
        if (!IsServer)
        {
            return;
        }

        SetState(CharacterState.Dead);
        NotifyDiedClientRpc();

        if (CharacterCollider != null)
        {
            CharacterCollider.enabled = false;
        }

        if (_autoDespawnOnDeath)
        {
            Invoke(nameof(DespawnSelf), _despawnDelay);
        }
    }

    [ClientRpc]
    private void NotifyDiedClientRpc()
    {
        OnDied?.Invoke();

        if (CharacterCollider != null)
        {
            CharacterCollider.enabled = false;
        }
    }

    private void DespawnSelf()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
