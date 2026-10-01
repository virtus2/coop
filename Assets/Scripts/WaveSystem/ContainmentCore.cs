using UnityEngine;
using Unity.Netcode;
using System;

public class ContainmentCore : NetworkBehaviour
{
    public static ContainmentCore Instance { get; private set; }

    public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(1000f);
    public float MaxHealth = 1000f;

    public Action<float, float> OnHealthChanged;
    public Action OnCoreDestroyed;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = MaxHealth;
        }

        CurrentHealth.OnValueChanged += (oldValue, newValue) =>
        {
            OnHealthChanged?.Invoke(newValue, MaxHealth);
            if (IsServer && newValue <= 0f && oldValue > 0f)
            {
                TriggerGameOver();
            }
        };
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        
        if (CurrentHealth.Value > 0)
        {
            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - damage);
        }
    }

    // 추후 유저가 고철 등을 소모하여 상호작용할 때 호출할 메서드
    public void Repair(float amount)
    {
        if (!IsServer) return;

        if (CurrentHealth.Value > 0)
        {
            CurrentHealth.Value = Mathf.Min(MaxHealth, CurrentHealth.Value + amount);
        }
    }

    private void TriggerGameOver()
    {
        Debug.LogWarning("[ContainmentCore] 격리 코어가 파괴되었습니다! 격리 실패 (GAME OVER)");
        OnCoreDestroyed?.Invoke();
        // TODO: 실제 게임 오버 UI 연출 및 결과창 띄우기
    }
}
