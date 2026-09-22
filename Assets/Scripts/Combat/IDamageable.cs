using UnityEngine;

/// <summary>
/// 피격 부위 분류 (헤드샷, 몸통, 약점 등)
/// </summary>
public enum HitboxType
{
    Body = 0,
    Head = 1,
    WeakPoint = 2,
    Limb = 3
}

/// <summary>
/// 피격 데미지 전달용 구조체입니다.
/// </summary>
public struct DamageInfo
{
    public int Amount;
    public ulong InstigatorClientId;
    public Vector3 HitPoint;
    public Vector3 HitNormal;
    public HitboxType HitboxType;
    public bool IsHeadshot => HitboxType == HitboxType.Head;
    public bool IsCharged;
    public float KnockbackForce;
    public Vector3 KnockbackDirection;

    public DamageInfo(
        int amount,
        ulong instigatorClientId,
        Vector3 hitPoint,
        Vector3 hitNormal,
        HitboxType hitboxType = HitboxType.Body,
        bool isCharged = false,
        float knockbackForce = 0f,
        Vector3 knockbackDirection = default)
    {
        Amount = amount;
        InstigatorClientId = instigatorClientId;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        HitboxType = hitboxType;
        IsCharged = isCharged;
        KnockbackForce = knockbackForce;
        KnockbackDirection = knockbackDirection;
    }
}

/// <summary>
/// 총기나 공격으로부터 데미지를 입을 수 있는 모든 엔티티(몬스터, 파괴 가능 오브젝트 등)가 구현하는 인터페이스입니다.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 데미지를 입히는 메서드입니다. 서버(IsServer) 권한으로 호출됩니다.
    /// </summary>
    /// <param name="damageInfo">피격 정보</param>
    void TakeDamage(DamageInfo damageInfo);

    /// <summary>
    /// 이미 사망했거나 파괴되었는지 여부
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// 대상의 Transform 컴포넌트
    /// </summary>
    Transform transform { get; }
}
