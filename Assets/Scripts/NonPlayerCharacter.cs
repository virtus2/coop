using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ?”ë“œ ?ì—???ìœ¨?ìœ¼ë¡??€ì§ì´ê±°ë‚˜ ?í˜¸?‘ìš©?˜ëŠ” ëª¬ìŠ¤??ë°?NPC??ë² ì´???´ë˜?¤ì…?ˆë‹¤.
///
/// [Authority vs Ownership ?¤ê³„ ?ì¹™]
/// 1. Server-Authoritative:
///    - ëª¨ë“  ?íƒœ ê²°ì •, ?´ë™, ?¼ê²© ë°??¬ë§ ?ì •?€ ?¤ì§ ?œë²„(IsServer)?ì„œë§??°ì‚° ë°?ê°±ì‹ ?©ë‹ˆ??
/// 2. Ownership??ëª…í™•??ë¶„ë¦¬:
///    - Unity Netcode for GameObjects(NGO)?ì„œ ?¸ìŠ¤??Host, ClientId = 0) ?˜ê²½????
///      ?œë²„ ?¤í°??ê°ì²´???´ë? OwnerClientIdê°€ ServerClientIdë¡??ë™ ì§€?•ë˜??///      ê¸°ìˆ ?ìœ¼ë¡?NGO??IsOwnerê°€ trueë¥?ë°˜í™˜?????ˆìŠµ?ˆë‹¤.
///    - ?˜ì?ë§???ìºë¦­?°ëŠ” ?Œë ˆ?´ì–´ê°€ ?Œìœ ?˜ê±°??ì¡°ì¢…?˜ëŠ” ìºë¦­?°ê? ?„ë‹™?ˆë‹¤.
///    - ?°ë¼??HasPlayerOwner, IsPlayerControlled????ƒ falseë¥?ë°˜í™˜?˜ë©°,
///      ?Œë ˆ?´ì–´ ?„ìš© ë¡œì§(PlayerController, ì¹´ë©”??ë¶€ì°? ?Œë ˆ?´ì–´ ?…ë ¥, HUD ????ê´€?¬í•˜ì§€ ?ŠìŠµ?ˆë‹¤.
/// </summary>
[DisallowMultipleComponent]
public abstract class NonPlayerCharacter : NetworkBehaviour, IDamageable
{
    [Header("Base Character Settings")]
    [SerializeField] private string _defaultName = "NonPlayerCharacter";
    [SerializeField] private int _maxHealth = 100;

    [Header("Stagger Settings (?¼ê²© ê²½ì§ ë°?ë¬´í•œ ?¤í„´ ë°©ì?)")]
    [Tooltip("?¼ê²© ??ê¸°ë³¸ ê²½ì§ ?œê°„(ì´?")]
    [SerializeField] private float _baseStaggerDuration = 0.35f;
    [Tooltip("?¨ì‹œê°??°ì† ?¼ê²© ??ê²½ì§ ?œê°„ ê°ì†Œ ë°°ìœ¨ (0.25ë©?ë§??¼ê²©ë§ˆë‹¤ 25%??ê°ì†Œ)")]
    [SerializeField] private float _staggerDiminishFactor = 0.25f;
    [Tooltip("?¼ê²©???†ì„ ??ê²½ì§ ?´ì„±??ì´ˆê¸°?”ë˜???€ê¸??œê°„(ì´?")]
    [SerializeField] private float _staggerResetDelay = 2.0f;

    private float _currentStaggerTimer;
    private int _consecutiveStaggerCount;
    private float _staggerResetTimer;

    public bool IsStaggered => _currentStaggerTimer > 0f;
    public float CurrentStaggerTimer => _currentStaggerTimer;

    [Header("Despawn Settings")]
    [SerializeField] private bool _autoDespawnOnDeath = true;
    [SerializeField] private float _despawnDelay = 3f;

    // --- ?Œìœ ê¶?ë°?ê¶Œí•œ ?ë³„ ?„ë¡œ?¼í‹° ---

    /// <summary>
    /// ?¤ì œ ?¸ê°„ ?Œë ˆ?´ì–´ê°€ ??ìºë¦­?°ë? ?Œìœ ?˜ê³  ?ˆëŠ”ì§€ ?¬ë??…ë‹ˆ??
    /// ëª¬ìŠ¤??ë°?NPC???œë²„ ?”í‹°?°ì´ë¯€ë¡???ƒ falseë¥?ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    public virtual bool HasPlayerOwner => false;

    /// <summary>
    /// ë¡œì»¬ ë¨¸ì‹ ???Œë ˆ?´ì–´ ?…ë ¥???˜í•´ ì¡°ì¢…?˜ëŠ”ì§€ ?¬ë??…ë‹ˆ??
    /// ëª¬ìŠ¤??ë°?NPC??AI ?ëŠ” ?œë²„ ?¤í¬ë¦½íŠ¸???˜í•´ ?œì–´?˜ë?ë¡???ƒ false?…ë‹ˆ??
    /// </summary>
    public virtual bool IsPlayerControlled => false;

    /// <summary>
    /// ?œë²„ê°€ ëª¨ë“  ë¡œì§ê³??íƒœë¥??„ì ?¼ë¡œ ?œì–´(Server-Authoritative)?˜ëŠ”ì§€ ?¬ë??…ë‹ˆ??
    /// </summary>
    public bool IsServerAuthoritative => true;

    // --- ?œë²„ ê¶Œí•œ ?¤íŠ¸?Œí¬ ?™ê¸°??ë³€??---

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

    // --- ?íƒœ ë°??´ë²¤??---

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
    /// ?´ë¼?´ì–¸??ì¸¡ì—???íƒœ ë³€ê²???ë¹„ì£¼??? ë‹ˆë©”ì´?? ?´í™??????ë°˜ì˜?˜ê¸° ?„í•œ ê°€??ë©”ì„œ?œì…?ˆë‹¤.
    /// </summary>
    protected virtual void OnClientStateChanged(CharacterState previousState, CharacterState newState)
    {
        // ?Œìƒ ?´ë˜?¤ì—??? ë‹ˆë©”ì´??ë°?ë¹„ì£¼??ì²˜ë¦¬
    }

    /// <summary>
    /// ?œë²„?ì„œë§??¤í–‰?˜ëŠ” ?íƒœ ë³€ê²?ë©”ì„œ?œì…?ˆë‹¤.
    /// </summary>
    /// <param name="newState">ë³€ê²½í•  ?ˆë¡œ???íƒœ</param>
    protected void SetState(CharacterState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[NonPlayerCharacter] ?íƒœ ë³€ê²½ì? ?¤ì§ ?œë²„?ì„œë§?ê°€?¥í•©?ˆë‹¤! (?¤ë¸Œ?íŠ¸: {name})");
            return;
        }

        if (_currentState.Value == CharacterState.Dead)
        {
            return; // ?´ë? ?¬ë§??ê²½ìš° ?íƒœ ë³€ê²?ë¬´ì‹œ
        }

        _currentState.Value = newState;
    }

    /// <summary>
    /// IDamageable ?¸í„°?˜ì´??êµ¬í˜„. ?¼ê²© ?•ë³´(?¤ë“œ?? ì°¨ì? ?¬ë? ??ë¥??¬í•¨?˜ì—¬ ?°ë?ì§€ë¥??ìš©?©ë‹ˆ??
    /// </summary>
    public virtual void TakeDamage(DamageInfo damageInfo)
    {
        if (!IsServer || IsDead || damageInfo.Amount <= 0) return;

        int previousHealth = _currentHealth.Value;
        int newHealth = Mathf.Max(0, previousHealth - damageInfo.Amount);
        _currentHealth.Value = newHealth;

        NotifyDamagedClientRpc(damageInfo.Amount, damageInfo.InstigatorClientId);

        if (newHealth <= 0)
        {
            HandleDeath(damageInfo);
            return; // Á×¾úÀ¸¸é °æÁ÷ÀÌ³ª ³Ë¹é »ı·« (RagdollÀÌ Ã³¸®ÇÔ)
        }

        ApplyStagger();
        if (damageInfo.KnockbackForce > 0f && damageInfo.KnockbackDirection != Vector3.zero)
        {
            ApplyKnockback(damageInfo.KnockbackDirection, damageInfo.KnockbackForce);
        }
    }

    protected virtual void ApplyKnockback(Vector3 direction, float force)
    {
    }

    public virtual void TakeDamage(int damage, ulong instigatorClientId)
    {
        TakeDamage(new DamageInfo(damage, instigatorClientId, transform.position, Vector3.up));
    }

    /// <summary>
    /// ?ê° ë²•ì¹™(Diminishing Returns)???ìš©?˜ì—¬ ê²½ì§ ?œê°„??ê³„ì‚°?˜ê³  ë¶€?¬í•©?ˆë‹¤.
    /// </summary>
    protected virtual void ApplyStagger()
    {
        if (!IsServer || IsDead) return;

        // ?ê° ë°°ìœ¨ ê³„ì‚°: 1?Œì°¨ 100%, 2?Œì°¨ 75%, 3?Œì°¨ 50% ... ìµœì†Œ 10%
        float multiplier = Mathf.Max(0.1f, 1.0f - (_consecutiveStaggerCount * _staggerDiminishFactor));
        float calculatedDuration = _baseStaggerDuration * multiplier;

        _currentStaggerTimer = Mathf.Max(_currentStaggerTimer, calculatedDuration);
        _consecutiveStaggerCount++;
        _staggerResetTimer = _staggerResetDelay;

        NotifyStaggeredClientRpc(calculatedDuration);
    }

    /// <summary>
    /// ?œë²„ ?„ë ˆ?„ë§ˆ??ê²½ì§ ?€?´ë¨¸?€ ?ê° ì´ˆê¸°???€?´ë¨¸ë¥?ê°±ì‹ ?©ë‹ˆ??
    /// </summary>
    protected virtual void UpdateStagger(float deltaTime)
    {
        if (!IsServer) return;

        if (_currentStaggerTimer > 0f)
        {
            _currentStaggerTimer -= deltaTime;
            if (_currentStaggerTimer <= 0f)
            {
                _currentStaggerTimer = 0f;
            }
        }

        if (_staggerResetTimer > 0f)
        {
            _staggerResetTimer -= deltaTime;
            if (_staggerResetTimer <= 0f)
            {
                // ?¼ì • ?œê°„ ?™ì•ˆ ì¶”ê? ?¼ê²©???†ì—ˆ?¼ë?ë¡??ê° ì¹´ìš´??ë¦¬ì…‹
                _consecutiveStaggerCount = 0;
            }
        }
    }

    [ClientRpc]
    private void NotifyStaggeredClientRpc(float duration)
    {
        // ?´ë¼?´ì–¸??ì¸??¼ê²© ê²½ì§ ? ë‹ˆë©”ì´???¬ìš´???¸ë¦¬ê±°ìš©
    }

    [ClientRpc]
    private void NotifyDamagedClientRpc(int damage, ulong instigatorClientId)
    {
        OnDamaged?.Invoke(damage, instigatorClientId);
    }

    /// <summary>
    /// ?¬ë§ ì²˜ë¦¬ ë¡œì§?…ë‹ˆ?? ?œë²„?ì„œ ?¸ì¶œ?©ë‹ˆ??
    /// </summary>
    protected virtual void HandleDeath(DamageInfo lastDamage)
    {
        if (!IsServer)
        {
            return;
        }

        SetState(CharacterState.Dead);
        NotifyDiedClientRpc(lastDamage.KnockbackDirection, lastDamage.KnockbackForce, lastDamage.HitPoint);

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
    private void NotifyDiedClientRpc(Vector3 knockbackDir, float knockbackForce, Vector3 hitPoint)
    {
        OnDied?.Invoke();
        OnClientDied(knockbackDir, knockbackForce, hitPoint);

        if (CharacterCollider != null)
        {
            CharacterCollider.enabled = false;
        }
    }

    protected virtual void OnClientDied(Vector3 knockbackDir, float knockbackForce, Vector3 hitPoint)
    {
    }

    private void DespawnSelf()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
