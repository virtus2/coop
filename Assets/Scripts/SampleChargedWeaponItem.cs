using UnityEngine;

/// <summary>
/// 클릭 시 단발 발사(IFireable) 및 홀드 시 충전 사용(IUsable)이 모두 가능한 복합 샘플 아이템입니다.
/// </summary>
public class SampleChargedWeaponItem : MonoBehaviour, IFireable, IUsable
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
