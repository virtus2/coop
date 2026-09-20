using UnityEngine;

/// <summary>
/// 단발 클릭 발사(IFireable)를 테스트하기 위한 샘플 아이템 컴포넌트입니다.
/// </summary>
public class SampleGunItem : MonoBehaviour, IFireable
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

