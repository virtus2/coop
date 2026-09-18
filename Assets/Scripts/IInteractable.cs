using UnityEngine;

/// <summary>
/// 상호작용 가능한 모든 인게임 오브젝트가 구현해야 하는 인터페이스입니다.
/// 버튼, 문, 집을 수 있는 물체 등 오브젝트별 상호작용 로직을 다형성으로 처리합니다.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 상호작용 키(기본: E키) 입력 시 실행되는 동작입니다.
    /// </summary>
    /// <param name="interactor">상호작용을 시도한 플레이어의 PlayerInteraction 컴포넌트</param>
    void Interact(PlayerInteraction interactor);

    /// <summary>
    /// 현재 해당 플레이어와 상호작용이 가능한 상태인지 여부를 반환합니다.
    /// </summary>
    /// <param name="interactor">상호작용을 시도하는 플레이어</param>
    /// <returns>상호작용 가능 여부</returns>
    bool CanInteract(PlayerInteraction interactor);

    /// <summary>
    /// 플레이어 화면의 상호작용 안내 UI에 표시할 문구를 반환합니다.
    /// 예: "들기", "상호작용", "열기", "누르기" 등
    /// </summary>
    /// <returns>상호작용 액션 텍스트</returns>
    string GetInteractionPrompt();
}
