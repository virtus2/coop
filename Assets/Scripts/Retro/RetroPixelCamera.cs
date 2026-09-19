using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP의 네이티브 RenderScale과 Point(Nearest-Neighbor) 업스케일링을 제어하여
/// 캔버스나 UI 오버레이 없이 3D 씬 전체를 순수하게 로우폴리 픽셀아트로 렌더링하는 카메라 컨트롤러입니다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class RetroPixelCamera : MonoBehaviour
{
    public enum RetroResolutionPreset
    {
        Preset180p_320x180,
        Preset270p_480x270,
        Preset360p_640x360,
        Preset480p_854x480,
        Custom,
        Native
    }

    [Header("Resolution Preset")]
    [SerializeField] private RetroResolutionPreset _preset = RetroResolutionPreset.Preset270p_480x270;
    [SerializeField] private int _customTargetHeight = 270;

    [Header("Free Camera Controls (Testing)")]
    [SerializeField] private bool _enableFreeCam = true;
    [SerializeField] private float _moveSpeed = 6.0f;
    [SerializeField] private float _lookSensitivity = 0.15f;

    private Camera _camera;
    private UniversalRenderPipelineAsset _urpAsset;
    private float _originalRenderScale = 1.0f;
    private UpscalingFilterSelection _originalUpscalingFilter = UpscalingFilterSelection.Auto;
    private bool _hasCachedOriginalSettings;
    private int _lastScreenHeight;
    private RetroResolutionPreset _lastPreset;
    private int _lastCustomHeight;
    private float _yaw;
    private float _pitch;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera != null)
        {
            _camera.targetTexture = null;
        }

        Vector3 angles = transform.eulerAngles;
        _pitch = angles.x;
        _yaw = angles.y;

        CleanupLegacyCanvas();
        CacheAndApplyUrpSettings();
    }

    private void OnEnable()
    {
        if (_camera != null)
        {
            _camera.targetTexture = null;
        }

        CleanupLegacyCanvas();
        CacheAndApplyUrpSettings();
    }

    private void OnDisable()
    {
        RestoreOriginalUrpSettings();
    }

    private void OnDestroy()
    {
        RestoreOriginalUrpSettings();
    }

    private void Update()
    {
        if (_preset != _lastPreset || _customTargetHeight != _lastCustomHeight || Screen.height != _lastScreenHeight)
        {
            ApplyResolutionScale();
        }

        if (_enableFreeCam && Application.isPlaying)
        {
            HandleFreeCamMovement();
        }
    }

    public void SetPreset(RetroResolutionPreset preset)
    {
        _preset = preset;
        ApplyResolutionScale();
    }

    private void CacheAndApplyUrpSettings()
    {
        _urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (_urpAsset == null)
        {
            _urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        }

        if (_urpAsset == null)
        {
            _urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        }

        if (_urpAsset != null && !_hasCachedOriginalSettings)
        {
            _originalRenderScale = _urpAsset.renderScale;
            _originalUpscalingFilter = _urpAsset.upscalingFilter;
            _hasCachedOriginalSettings = true;
        }

        ApplyResolutionScale();
    }

    private void ApplyResolutionScale()
    {
        if (_urpAsset == null)
        {
            return;
        }

        _lastPreset = _preset;
        _lastCustomHeight = _customTargetHeight;
        _lastScreenHeight = Screen.height;

        if (_preset == RetroResolutionPreset.Native)
        {
            _urpAsset.renderScale = 1.0f;
            _urpAsset.upscalingFilter = UpscalingFilterSelection.Auto;
            return;
        }

        int targetHeight = GetTargetHeight();
        int screenH = Screen.height > 0 ? Screen.height : 1080;

        float calculatedScale = (float)targetHeight / screenH;
        float targetScale = Mathf.Clamp(calculatedScale, 0.1f, 1.0f);

        _urpAsset.renderScale = targetScale;
        _urpAsset.upscalingFilter = UpscalingFilterSelection.Point;
    }

    private int GetTargetHeight()
    {
        switch (_preset)
        {
            case RetroResolutionPreset.Preset180p_320x180:
                return 180;
            case RetroResolutionPreset.Preset270p_480x270:
                return 270;
            case RetroResolutionPreset.Preset360p_640x360:
                return 360;
            case RetroResolutionPreset.Preset480p_854x480:
                return 480;
            case RetroResolutionPreset.Custom:
                return Mathf.Max(100, _customTargetHeight);
            default:
                return 270;
        }
    }

    private void RestoreOriginalUrpSettings()
    {
        if (_hasCachedOriginalSettings && _urpAsset != null)
        {
            _urpAsset.renderScale = _originalRenderScale;
            _urpAsset.upscalingFilter = _originalUpscalingFilter;
        }
    }

    private void CleanupLegacyCanvas()
    {
        GameObject legacyCanvas = GameObject.Find("RetroDisplayCanvas");
        if (legacyCanvas != null)
        {
            DestroyImmediate(legacyCanvas);
        }
    }

    private void HandleFreeCamMovement()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || mouse == null)
        {
            return;
        }

        // Mouse look while holding right-click or left-click
        if (mouse.rightButton.isPressed || mouse.leftButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            _yaw += mouseDelta.x * _lookSensitivity;
            _pitch -= mouseDelta.y * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // Movement with WASD + Space/Ctrl
        Vector3 moveDirection = Vector3.zero;

        if (keyboard.wKey.isPressed)
        {
            moveDirection += transform.forward;
        }

        if (keyboard.sKey.isPressed)
        {
            moveDirection -= transform.forward;
        }

        if (keyboard.dKey.isPressed)
        {
            moveDirection += transform.right;
        }

        if (keyboard.aKey.isPressed)
        {
            moveDirection -= transform.right;
        }

        if (keyboard.spaceKey.isPressed)
        {
            moveDirection += Vector3.up;
        }

        if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed)
        {
            moveDirection -= Vector3.up;
        }

        float speed = _moveSpeed;
        if (keyboard.leftShiftKey.isPressed)
        {
            speed *= 2.0f;
        }

        transform.position += moveDirection.normalized * (speed * Time.deltaTime);
    }
}
