using System;
using UnityEngine;

/// <summary>
/// 게임의 전반적인 환경 설정(마우스 감도 등)을 관리하고 PlayerPrefs에 저장/로드하는 정적 매니저 클래스입니다.
/// </summary>
public static class SettingsManager
{
    private const string KEY_MOUSE_SENSITIVITY = "Coop_MouseSensitivity";
    private const string KEY_MASTER_VOLUME = "Coop_MasterVolume";
    private const string KEY_BGM_VOLUME = "Coop_BgmVolume";
    private const string KEY_SFX_VOLUME = "Coop_SfxVolume";

    public const float DEFAULT_MOUSE_SENSITIVITY = 0.1f;
    public const float MIN_MOUSE_SENSITIVITY = 0.01f;
    public const float MAX_MOUSE_SENSITIVITY = 0.50f;

    public const float DEFAULT_MASTER_VOLUME = 1.0f;
    public const float DEFAULT_BGM_VOLUME = 0.8f;
    public const float DEFAULT_SFX_VOLUME = 1.0f;

    private static float? _cachedMouseSensitivity;
    private static float? _cachedMasterVolume;
    private static float? _cachedBgmVolume;
    private static float? _cachedSfxVolume;

    /// <summary>
    /// 마우스 감도가 변경되었을 때 호출되는 이벤트입니다. (새로운 감도 값 전달)
    /// </summary>
    public static event Action<float> OnMouseSensitivityChanged;

    /// <summary>
    /// 마스터 볼륨이 변경되었을 때 호출되는 이벤트입니다. (0~1)
    /// </summary>
    public static event Action<float> OnMasterVolumeChanged;

    /// <summary>
    /// 배경음(BGM) 볼륨이 변경되었을 때 호출되는 이벤트입니다. (0~1)
    /// </summary>
    public static event Action<float> OnBgmVolumeChanged;

    /// <summary>
    /// 효과음(SFX) 볼륨이 변경되었을 때 호출되는 이벤트입니다. (0~1)
    /// </summary>
    public static event Action<float> OnSfxVolumeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _cachedMouseSensitivity = null;
        _cachedMasterVolume = null;
        _cachedBgmVolume = null;
        _cachedSfxVolume = null;

        OnMouseSensitivityChanged = null;
        OnMasterVolumeChanged = null;
        OnBgmVolumeChanged = null;
        OnSfxVolumeChanged = null;
    }

    /// <summary>
    /// 현재 설정된 마우스 감도입니다. 값을 변경하면 PlayerPrefs에 저장되고 이벤트가 발행됩니다.
    /// </summary>
    public static float MouseSensitivity
    {
        get
        {
            if (!_cachedMouseSensitivity.HasValue)
            {
                _cachedMouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, DEFAULT_MOUSE_SENSITIVITY);
                _cachedMouseSensitivity = Mathf.Clamp(_cachedMouseSensitivity.Value, MIN_MOUSE_SENSITIVITY, MAX_MOUSE_SENSITIVITY);
            }
            return _cachedMouseSensitivity.Value;
        }
        set
        {
            float clampedValue = Mathf.Clamp(value, MIN_MOUSE_SENSITIVITY, MAX_MOUSE_SENSITIVITY);
            if (_cachedMouseSensitivity.HasValue && Mathf.Approximately(_cachedMouseSensitivity.Value, clampedValue))
            {
                return;
            }

            _cachedMouseSensitivity = clampedValue;
            PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, clampedValue);
            PlayerPrefs.Save();

            OnMouseSensitivityChanged?.Invoke(clampedValue);
        }
    }

    /// <summary>
    /// 현재 설정된 마스터 볼륨(0~1)입니다.
    /// </summary>
    public static float MasterVolume
    {
        get
        {
            if (!_cachedMasterVolume.HasValue)
            {
                _cachedMasterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, DEFAULT_MASTER_VOLUME);
                _cachedMasterVolume = Mathf.Clamp01(_cachedMasterVolume.Value);
            }
            return _cachedMasterVolume.Value;
        }
        set
        {
            float clampedValue = Mathf.Clamp01(value);
            if (_cachedMasterVolume.HasValue && Mathf.Approximately(_cachedMasterVolume.Value, clampedValue))
            {
                return;
            }

            _cachedMasterVolume = clampedValue;
            PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, clampedValue);
            PlayerPrefs.Save();

            OnMasterVolumeChanged?.Invoke(clampedValue);
        }
    }

    /// <summary>
    /// 현재 설정된 배경음(BGM) 볼륨(0~1)입니다.
    /// </summary>
    public static float BgmVolume
    {
        get
        {
            if (!_cachedBgmVolume.HasValue)
            {
                _cachedBgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, DEFAULT_BGM_VOLUME);
                _cachedBgmVolume = Mathf.Clamp01(_cachedBgmVolume.Value);
            }
            return _cachedBgmVolume.Value;
        }
        set
        {
            float clampedValue = Mathf.Clamp01(value);
            if (_cachedBgmVolume.HasValue && Mathf.Approximately(_cachedBgmVolume.Value, clampedValue))
            {
                return;
            }

            _cachedBgmVolume = clampedValue;
            PlayerPrefs.SetFloat(KEY_BGM_VOLUME, clampedValue);
            PlayerPrefs.Save();

            OnBgmVolumeChanged?.Invoke(clampedValue);
        }
    }

    /// <summary>
    /// 현재 설정된 효과음(SFX) 볼륨(0~1)입니다.
    /// </summary>
    public static float SfxVolume
    {
        get
        {
            if (!_cachedSfxVolume.HasValue)
            {
                _cachedSfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, DEFAULT_SFX_VOLUME);
                _cachedSfxVolume = Mathf.Clamp01(_cachedSfxVolume.Value);
            }
            return _cachedSfxVolume.Value;
        }
        set
        {
            float clampedValue = Mathf.Clamp01(value);
            if (_cachedSfxVolume.HasValue && Mathf.Approximately(_cachedSfxVolume.Value, clampedValue))
            {
                return;
            }

            _cachedSfxVolume = clampedValue;
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, clampedValue);
            PlayerPrefs.Save();

            OnSfxVolumeChanged?.Invoke(clampedValue);
        }
    }
}
