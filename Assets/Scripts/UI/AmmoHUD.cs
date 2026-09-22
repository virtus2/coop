using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 우측 하단에 현재 총기의 탄창 잔탄수 및 인벤토리 예비 탄약수를 표시하는 HUD 컴포넌트입니다.
/// 총을 들었을 때 활성화되며, 재장전(RELOADING...) 및 차징 상태(CHARGE %)도 직관적으로 표시합니다.
/// </summary>
public class AmmoHUD : MonoBehaviour
{
    public static AmmoHUD Instance { get; private set; }

    [Header("UI Root")]
    [SerializeField] private GameObject _ammoPanel;

    [Header("Text Display")]
    [SerializeField] private TextMeshProUGUI _ammoText;
    [SerializeField] private TextMeshProUGUI _statusText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_ammoPanel != null)
        {
            _ammoPanel.SetActive(false);
        }
    }

    private void Start()
    {
        UpdateHUD();
    }

    private void Update()
    {
        UpdateHUD();
    }

    /// <summary>
    /// PlayerGunCombat의 현재 상태를 읽어 UI를 갱신합니다.
    /// </summary>
    public void UpdateHUD()
    {
        PlayerGunCombat combat = PlayerGunCombat.LocalInstance;
        if (combat == null || !combat.HasGunEquipped)
        {
            if (_ammoPanel != null && _ammoPanel.activeSelf)
            {
                _ammoPanel.SetActive(false);
            }
            return;
        }

        if (_ammoPanel != null && !_ammoPanel.activeSelf)
        {
            _ammoPanel.SetActive(true);
        }

        if (_ammoText != null)
        {
            int current = combat.CurrentAmmoInClip;
            int total = combat.TotalReserveAmmo;
            int maxCap = combat.CurrentGunData != null ? combat.CurrentGunData.MagazineCapacity : 0;

            // 탄약 부족 시 붉은색 경고 강조
            if (current <= 0)
            {
                _ammoText.text = $"<color=#FF4444>0</color> <size=65%>/ {total}</size>";
            }
            else
            {
                _ammoText.text = $"<color=#FFFFFF>{current}</color> <size=65%>/ {total}</size>";
            }
        }

        if (_statusText != null)
        {
            if (combat.IsReloading)
            {
                int pct = Mathf.RoundToInt(combat.ReloadProgress * 100f);
                _statusText.text = $"<color=#FFAA00>RELOADING... ({pct}%)</color>";
                _statusText.gameObject.SetActive(true);
            }
            else if (combat.IsCharging)
            {
                int pct = Mathf.RoundToInt(combat.ChargeProgress * 100f);
                string color = pct >= 100 ? "#00FFFF" : "#FFFF00";
                _statusText.text = $"<color={color}>CHARGING ({pct}%)</color>";
                _statusText.gameObject.SetActive(true);
            }
            else
            {
                _statusText.gameObject.SetActive(false);
            }
        }
    }
    /// <summary>
    /// 씬에 배치된 AmmoHUD 인스턴스를 찾거나 반환합니다.
    /// </summary>
    public static AmmoHUD EnsureInstance()
    {
        if (Instance == null)
        {
            Instance = FindFirstObjectByType<AmmoHUD>();
        }
        return Instance;
    }
}
