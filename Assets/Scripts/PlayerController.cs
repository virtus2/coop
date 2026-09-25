/// <summary>
/// 하위 호환성을 제공하는 PlayerController 컴포넌트입니다.
/// 모든 캐릭터 로직은 PlayerCharacter 기반 클래스에서 처리됩니다.
/// </summary>
public class PlayerController : PlayerCharacter
{
    public static new PlayerController LocalInstance => PlayerCharacter.LocalInstance as PlayerController;
}
