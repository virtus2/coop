using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 환경 설정(마우스 감도 등)을 조절할 수 있는 재사용 가능한 모듈형 옵션 창 UI 컨트롤러입니다.
/// 인게임 메뉴뿐만 아니라 메인 메뉴 등 다양한 씬에서 Prefab으로 배치하여 사용할 수 있습니다.
/// </summary>
public class OptionWindowUI : MonoBehaviour
{
    [Header("UI Root")]
    [Tooltip("옵션창 패널 루트 오브젝트입니다. 비어있으면 이 컴포넌트의 gameObject를 기준으로 토글합니다.")]
    [SerializeField] private GameObject _windowPanel;

    [Header("Mouse Sensitivity UI")]
    [SerializeField] private Slider _mouseSensitivitySlider;
    [SerializeField] private Text _mouseSensitivityValueText;

    [Header("Audio Volume UI")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Text _masterVolumeValueText;
    [SerializeField] private Slider _bgmVolumeSlider;
    [SerializeField] private Text _bgmVolumeValueText;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Text _sfxVolumeValueText;

    [Header("Buttons")]
    [SerializeField] private Button _closeButton;

    /// <summary>
    /// 옵션창이 닫힐 때 발생하는 이벤트입니다. (이전 메뉴 복귀 등에 활용)
    /// </summary>
    public event Action OnClosed;

    public bool IsOpen
    {
        get
        {
            if (_windowPanel != null)
            {
                return _windowPanel.activeSelf;
            }
            return gameObject.activeSelf;
        }
    }

    private bool _isInitialized = false;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        RegisterListeners();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_windowPanel == null)
        {
            _windowPanel = gameObject;
        }

        _isInitialized = true;
    }

    private void RegisterListeners()
    {
        if (_mouseSensitivitySlider != null)
        {
            _mouseSensitivitySlider.minValue = SettingsManager.MIN_MOUSE_SENSITIVITY;
            _mouseSensitivitySlider.maxValue = SettingsManager.MAX_MOUSE_SENSITIVITY;
            _mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivitySliderChanged);
            _mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivitySliderChanged);
        }

        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.minValue = 0f;
            _masterVolumeSlider.maxValue = 1f;
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        }

        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.minValue = 0f;
            _bgmVolumeSlider.maxValue = 1f;
            _bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeSliderChanged);
            _bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeSliderChanged);
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.minValue = 0f;
            _sfxVolumeSlider.maxValue = 1f;
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeSliderChanged);
            _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeSliderChanged);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }

        SettingsManager.OnMouseSensitivityChanged -= HandleMouseSensitivityChanged;
        SettingsManager.OnMouseSensitivityChanged += HandleMouseSensitivityChanged;

        SettingsManager.OnMasterVolumeChanged -= HandleMasterVolumeChanged;
        SettingsManager.OnMasterVolumeChanged += HandleMasterVolumeChanged;

        SettingsManager.OnBgmVolumeChanged -= HandleBgmVolumeChanged;
        SettingsManager.OnBgmVolumeChanged += HandleBgmVolumeChanged;

        SettingsManager.OnSfxVolumeChanged -= HandleSfxVolumeChanged;
        SettingsManager.OnSfxVolumeChanged += HandleSfxVolumeChanged;
    }

    private void UnregisterListeners()
    {
        if (_mouseSensitivitySlider != null)
        {
            _mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivitySliderChanged);
        }

        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
        }

        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeSliderChanged);
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeSliderChanged);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        SettingsManager.OnMouseSensitivityChanged -= HandleMouseSensitivityChanged;
        SettingsManager.OnMasterVolumeChanged -= HandleMasterVolumeChanged;
        SettingsManager.OnBgmVolumeChanged -= HandleBgmVolumeChanged;
        SettingsManager.OnSfxVolumeChanged -= HandleSfxVolumeChanged;
    }

    public void Open()
    {
        EnsureInitialized();

        if (_windowPanel != null)
        {
            _windowPanel.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        RegisterListeners();
        RefreshUI();
    }

    public void Close()
    {
        if (_windowPanel != null)
        {
            _windowPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        UnregisterListeners();
        OnClosed?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void RefreshUI()
    {
        float currentSensitivity = SettingsManager.MouseSensitivity;
        if (_mouseSensitivitySlider != null)
        {
            _mouseSensitivitySlider.SetValueWithoutNotify(currentSensitivity);
        }
        UpdateSensitivityValueText(currentSensitivity);

        float currentMaster = SettingsManager.MasterVolume;
        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.SetValueWithoutNotify(currentMaster);
        }
        UpdateVolumeValueText(_masterVolumeValueText, currentMaster);

        float currentBgm = SettingsManager.BgmVolume;
        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.SetValueWithoutNotify(currentBgm);
        }
        UpdateVolumeValueText(_bgmVolumeValueText, currentBgm);

        float currentSfx = SettingsManager.SfxVolume;
        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.SetValueWithoutNotify(currentSfx);
        }
        UpdateVolumeValueText(_sfxVolumeValueText, currentSfx);
    }

    private void OnMouseSensitivitySliderChanged(float value)
    {
        SettingsManager.MouseSensitivity = value;
        UpdateSensitivityValueText(value);
    }

    private void OnMasterVolumeSliderChanged(float value)
    {
        SettingsManager.MasterVolume = value;
        UpdateVolumeValueText(_masterVolumeValueText, value);
    }

    private void OnBgmVolumeSliderChanged(float value)
    {
        SettingsManager.BgmVolume = value;
        UpdateVolumeValueText(_bgmVolumeValueText, value);
    }

    private void OnSfxVolumeSliderChanged(float value)
    {
        SettingsManager.SfxVolume = value;
        UpdateVolumeValueText(_sfxVolumeValueText, value);
    }

    private void HandleMouseSensitivityChanged(float newSensitivity)
    {
        if (_mouseSensitivitySlider != null && !Mathf.Approximately(_mouseSensitivitySlider.value, newSensitivity))
        {
            _mouseSensitivitySlider.SetValueWithoutNotify(newSensitivity);
        }
        UpdateSensitivityValueText(newSensitivity);
    }

    private void HandleMasterVolumeChanged(float newVolume)
    {
        if (_masterVolumeSlider != null && !Mathf.Approximately(_masterVolumeSlider.value, newVolume))
        {
            _masterVolumeSlider.SetValueWithoutNotify(newVolume);
        }
        UpdateVolumeValueText(_masterVolumeValueText, newVolume);
    }

    private void HandleBgmVolumeChanged(float newVolume)
    {
        if (_bgmVolumeSlider != null && !Mathf.Approximately(_bgmVolumeSlider.value, newVolume))
        {
            _bgmVolumeSlider.SetValueWithoutNotify(newVolume);
        }
        UpdateVolumeValueText(_bgmVolumeValueText, newVolume);
    }

    private void HandleSfxVolumeChanged(float newVolume)
    {
        if (_sfxVolumeSlider != null && !Mathf.Approximately(_sfxVolumeSlider.value, newVolume))
        {
            _sfxVolumeSlider.SetValueWithoutNotify(newVolume);
        }
        UpdateVolumeValueText(_sfxVolumeValueText, newVolume);
    }

    private void UpdateSensitivityValueText(float value)
    {
        if (_mouseSensitivityValueText != null)
        {
            _mouseSensitivityValueText.text = $"{value:0.00}";
        }
    }

    private void UpdateVolumeValueText(Text label, float value)
    {
        if (label != null)
        {
            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
