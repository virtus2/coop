using UnityEngine;

/// <summary>
/// 단발 클릭 발사(IFireable)를 테스트하기 위한 샘플 아이템 컴포넌트입니다.
/// </summary>
public class SampleGunItem : PickableItem, IFireable
{
    [Header("Gun Settings")]
    [SerializeField] private string _weaponName = "샘플 권총";

    public bool CanFire(PlayerInteraction player)
    {
        return true;
    }

    public void Fire(PlayerInteraction player)
    {
        Debug.Log($"<color=orange>[SampleGunItem]</color> '{_weaponName}' 발사! 빵! (플레이어: {player.name})");
    }
}

/// <summary>
/// 홀드 사용(IUsable)을 테스트하기 위한 샘플 아이템 컴포넌트입니다.
/// 1.5초 동안 누르고 있으면 사용이 완료됩니다.
/// </summary>
public class SampleMedkitItem : PickableItem, IUsable
{
    [Header("Medkit Settings")]
    [SerializeField] private string _itemName = "샘플 구급키트";
    [SerializeField] private float _requiredHoldDuration = 1.5f;

    private float _lastLogTime;

    public float RequiredHoldDuration => _requiredHoldDuration;

    public bool CanUse(PlayerInteraction player)
    {
        return true;
    }

    public void OnUseStart(PlayerInteraction player)
    {
        _lastLogTime = 0f;
        Debug.Log($"<color=green>[SampleMedkitItem]</color> '{_itemName}' 사용 시작... (홀드 필요 시간: {_requiredHoldDuration:F1}초)");
    }

    public void OnUseUpdate(PlayerInteraction player, float holdDuration)
    {
        // 너무 많은 로그가 찍히지 않도록 0.3초마다 한 번씩 디버그 로그 출력
        if (holdDuration - _lastLogTime >= 0.3f)
        {
            _lastLogTime = holdDuration;
            float progress = Mathf.Clamp01(holdDuration / _requiredHoldDuration) * 100f;
            Debug.Log($"<color=green>[SampleMedkitItem]</color> '{_itemName}' 사용 중... [{holdDuration:F1}s / {_requiredHoldDuration:F1}s] ({progress:F0}%)");
        }
    }

    public void OnUseEnd(PlayerInteraction player, bool isCompleted)
    {
        if (isCompleted)
        {
            Debug.Log($"<color=green>[SampleMedkitItem]</color> '{_itemName}' 사용 완료! 치료 성공!");
        }
        else
        {
            Debug.Log($"<color=yellow>[SampleMedkitItem]</color> '{_itemName}' 사용 취소/중단됨.");
        }
    }
}

/// <summary>
/// 클릭 시 단발 발사(IFireable) 및 홀드 시 충전 사용(IUsable)이 모두 가능한 복합 샘플 아이템입니다.
/// </summary>
public class SampleChargedWeaponItem : PickableItem, IFireable, IUsable
{
    [Header("Charged Weapon Settings")]
    [SerializeField] private string _weaponName = "샘플 차지 라이플";
    [SerializeField] private float _requiredHoldDuration = 2.0f;

    private float _lastLogTime;

    public float RequiredHoldDuration => _requiredHoldDuration;

    public bool CanFire(PlayerInteraction player)
    {
        return true;
    }

    public void Fire(PlayerInteraction player)
    {
        Debug.Log($"<color=cyan>[SampleChargedWeaponItem]</color> '{_weaponName}' 기본 빔 발사 (탭 공격)! 슝~");
    }

    public bool CanUse(PlayerInteraction player)
    {
        return true;
    }

    public void OnUseStart(PlayerInteraction player)
    {
        _lastLogTime = 0f;
        Debug.Log($"<color=magenta>[SampleChargedWeaponItem]</color> '{_weaponName}' 충전 모드 시작... (차징 필요 시간: {_requiredHoldDuration:F1}초)");
    }

    public void OnUseUpdate(PlayerInteraction player, float holdDuration)
    {
        if (holdDuration - _lastLogTime >= 0.3f)
        {
            _lastLogTime = holdDuration;
            float progress = Mathf.Clamp01(holdDuration / _requiredHoldDuration) * 100f;
            Debug.Log($"<color=magenta>[SampleChargedWeaponItem]</color> '{_weaponName}' 에너지 차징 중... ({progress:F0}%)");
        }
    }

    public void OnUseEnd(PlayerInteraction player, bool isCompleted)
    {
        if (isCompleted)
        {
            Debug.Log($"<color=magenta>[SampleChargedWeaponItem]</color> '{_weaponName}' 충전 완료! 강력한 메가 레이저 방출!!!");
        }
        else
        {
            Debug.Log($"<color=yellow>[SampleChargedWeaponItem]</color> '{_weaponName}' 충전 중단됨.");
        }
    }
}
