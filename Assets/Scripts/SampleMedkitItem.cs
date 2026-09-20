using UnityEngine;

/// <summary>
/// 홀드 사용(IUsable)을 테스트하기 위한 샘플 아이템 컴포넌트입니다.
/// 1.5초 동안 누르고 있으면 사용이 완료됩니다.
/// </summary>
public class SampleMedkitItem : MonoBehaviour, IUsable
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
