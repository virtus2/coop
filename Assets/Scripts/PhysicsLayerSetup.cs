using UnityEngine;

/// <summary>
/// 물리 시뮬레이션 성능 최적화를 위해 레이어 간 충돌 매트릭스를 런타임에 자동 설정합니다.
/// 수백 개의 PickableItem이 바닥에 떨어져도 서로 부딪히지 않도록
/// 'PickableItem' 레이어 간 충돌(Item vs Item)을 비활성화하여 O(N^2) 물리 연산 및 지터링을 원천 차단합니다.
/// 지형(Default/Ground) 및 플레이어와의 상호작용 레이캐스트는 정상 유지됩니다.
/// </summary>
public static class PhysicsLayerSetup
{
    public const string PICKABLE_LAYER_NAME = "PickableItem";
    public const string PLAYER_LAYER_NAME = "Player";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        // Domain Reload 비활성화 대응
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializePhysicsLayers()
    {
        int pickableLayer = LayerMask.NameToLayer(PICKABLE_LAYER_NAME);
        if (pickableLayer < 0)
        {
            Debug.LogWarning($"[PhysicsLayerSetup] '{PICKABLE_LAYER_NAME}' 레이어가 등록되어 있지 않습니다. 기본 충돌 설정을 유지합니다.");
            return;
        }

        // 1. 아이템끼리의 충돌 비활성화 (지터링 방지 및 물리 CPU 절감)
        Physics.IgnoreLayerCollision(pickableLayer, pickableLayer, true);

        // 2. 아이템과 플레이어(캐릭터) 간 충돌 비활성화 (던질 때 및 이동 시 캐릭터와 충돌 방지)
        int playerLayer = LayerMask.NameToLayer(PLAYER_LAYER_NAME);
        if (playerLayer >= 0)
        {
            Physics.IgnoreLayerCollision(pickableLayer, playerLayer, true);
            Debug.Log($"<color=cyan>[PhysicsLayerSetup]</color> '{PICKABLE_LAYER_NAME}' 레이어 간 및 '{PLAYER_LAYER_NAME}' 레이어와의 충돌을 비활성화했습니다.");
        }
        else
        {
            Debug.LogWarning($"[PhysicsLayerSetup] '{PLAYER_LAYER_NAME}' 레이어가 등록되어 있지 않습니다.");
        }
    }
}
