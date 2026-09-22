using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 총기 사격 또는 공격에 의해 데미지를 입고 파괴될 수 있는 월드 오브젝트입니다.
/// Server-Authoritative로 동작하며, 체력이 0이 되면 파괴 효과를 재생하고 Despawn/파괴됩니다.
/// </summary>
public class DestructibleObject : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 50;
    [SerializeField] private GameObject _destructionEffectPrefab;

    private readonly NetworkVariable<int> _currentHealth = new NetworkVariable<int>(
        50,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsDead => _currentHealth.Value <= 0;
    public int CurrentHealth => _currentHealth.Value;
    public int MaxHealth => _maxHealth;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _currentHealth.Value = _maxHealth;
        }
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!IsServer || IsDead)
        {
            return;
        }

        int newHealth = Mathf.Max(0, _currentHealth.Value - damageInfo.Amount);
        _currentHealth.Value = newHealth;

        Debug.Log($"[DestructibleObject] '{name}' 피격! 데미지: {damageInfo.Amount}, 잔여 체력: {newHealth}/{_maxHealth}");

        if (newHealth <= 0)
        {
            HandleDestruction(damageInfo.HitPoint);
        }
    }

    private void HandleDestruction(Vector3 hitPoint)
    {
        PlayDestructionEffectClientRpc(hitPoint);

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void PlayDestructionEffectClientRpc(Vector3 hitPoint)
    {
        if (_destructionEffectPrefab != null)
        {
            Instantiate(_destructionEffectPrefab, hitPoint, Quaternion.identity);
        }
    }
}
