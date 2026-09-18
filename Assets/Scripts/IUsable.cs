/// <summary>
/// 좌클릭을 홀드하여 사용(Hold/Use) 동작을 수행하는 아이템이 구현하는 인터페이스입니다.
/// </summary>
public interface IUsable
{
    /// <summary>
    /// 아이템 사용에 필요한 최소 홀드 시간(초)입니다.
    /// 0 이하인 경우 홀드 유지 시간 동안 매 프레임 지속 사용(채널링/스프레이 등) 형태를 의미합니다.
    /// </summary>
    float RequiredHoldDuration { get; }

    /// <summary>
    /// 현재 사용이 가능한 상태인지 여부를 반환합니다.
    /// </summary>
    /// <param name="player">사용을 시도하는 플레이어 상호작용 컴포넌트</param>
    /// <returns>사용 가능 여부</returns>
    bool CanUse(PlayerInteraction player);

    /// <summary>
    /// 홀드 사용을 시작할 때 호출됩니다.
    /// </summary>
    /// <param name="player">사용하는 플레이어 상호작용 컴포넌트</param>
    void OnUseStart(PlayerInteraction player);

    /// <summary>
    /// 홀드를 유지하는 동안 매 프레임 호출됩니다.
    /// </summary>
    /// <param name="player">사용하는 플레이어 상호작용 컴포넌트</param>
    /// <param name="holdDuration">현재까지 홀드된 시간(초)</param>
    void OnUseUpdate(PlayerInteraction player, float holdDuration);

    /// <summary>
    /// 홀드가 종료(키를 뗐거나 취소/완료)되었을 때 호출됩니다.
    /// </summary>
    /// <param name="player">사용하는 플레이어 상호작용 컴포넌트</param>
    /// <param name="isCompleted">필요 홀드 시간을 충족하여 정상 완료되었는지 여부</param>
    void OnUseEnd(PlayerInteraction player, bool isCompleted);
}
