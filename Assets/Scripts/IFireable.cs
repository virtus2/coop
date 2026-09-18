/// <summary>
/// 좌클릭을 통해 발사(Fire/Shoot) 동작을 수행하는 아이템이 구현하는 인터페이스입니다.
/// </summary>
public interface IFireable
{
    /// <summary>
    /// 현재 발사가 가능한 상태인지 여부를 반환합니다.
    /// </summary>
    /// <param name="player">발사를 시도하는 플레이어 상호작용 컴포넌트</param>
    /// <returns>발사 가능 여부</returns>
    bool CanFire(PlayerInteraction player);

    /// <summary>
    /// 발사 동작을 실행합니다.
    /// </summary>
    /// <param name="player">발사를 실행하는 플레이어 상호작용 컴포넌트</param>
    void Fire(PlayerInteraction player);
}
