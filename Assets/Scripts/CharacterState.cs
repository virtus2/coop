/// <summary>
/// 월드 상의 몬스터 및 NPC의 현재 행동 상태를 나타내는 열거형입니다.
/// 서버에서 상태를 결정하고 NetworkVariable을 통해 클라이언트에 동기화됩니다.
/// </summary>
public enum CharacterState : byte
{
    /// <summary>
    /// 기본 정지/대기 상태
    /// </summary>
    Idle = 0,

    /// <summary>
    /// 지정된 경로나 주변을 배회하는 순찰 상태
    /// </summary>
    Patrol = 1,

    /// <summary>
    /// 플레이어 또는 대상을 추적하는 상태
    /// </summary>
    Chase = 2,

    /// <summary>
    /// 대상을 공격하는 상태
    /// </summary>
    Attack = 3,

    /// <summary>
    /// 플레이어와 상호작용(대화 등) 중인 상태
    /// </summary>
    Interacting = 4,

    /// <summary>
    /// 피격 또는 경직 상태
    /// </summary>
    Stunned = 5,

    /// <summary>
    /// 사망 상태
    /// </summary>
    Dead = 6
}
