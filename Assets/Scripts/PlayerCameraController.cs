using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 로컬 클라이언트 전용 카메라 컨트롤러입니다.
/// 캐릭터가 존재할 때는 캐릭터의 1인칭 머리(CameraTarget)를 추적하고,
/// 캐릭터가 없을 때(사망/스폰 대기 중)는 기지 격리 코어(Containment Core)를 비추는 고정 시점으로 자동 전환합니다.
/// </summary>
public class PlayerCameraController : MonoBehaviour
{
    public static PlayerCameraController Instance { get; private set; }

    [Header("Cinemachine Camera")]
    [SerializeField] private CinemachineCamera _virtualCamera;

    [Header("Spectate / Core Settings")]
    [Tooltip("캐릭터가 없을 때 비출 기지 방어 코어 또는 고정 관전 위치")]
    [SerializeField] private Transform _coreSpectateTarget;

    private Transform _currentCharacterTarget;
    private float _defaultFov = 60f;
    private bool _isInitialized;

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
        EnsureCamera();
    }

    private void EnsureCamera()
    {
        if (_virtualCamera == null)
        {
            _virtualCamera = GetComponentInChildren<CinemachineCamera>(true);
            if (_virtualCamera == null)
            {
                _virtualCamera = FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
            }
        }

        if (_virtualCamera != null)
        {
            _defaultFov = _virtualCamera.Lens.FieldOfView;
            _isInitialized = true;
        }
    }

    /// <summary>
    /// 캐릭터의 1인칭 머리 타겟을 추적하도록 설정합니다.
    /// </summary>
    public void SetCharacterTarget(Transform cameraTarget)
    {
        EnsureCamera();
        _currentCharacterTarget = cameraTarget;

        if (_virtualCamera == null || cameraTarget == null)
        {
            return;
        }

        _virtualCamera.gameObject.SetActive(true);
        _virtualCamera.Priority.Value = 20;

        // Cinemachine 3.x의 Target 설정
        _virtualCamera.Target.TrackingTarget = cameraTarget;
        _virtualCamera.Target.LookAtTarget = cameraTarget;

        // 카메라 위치를 즉시 동기화
        _virtualCamera.transform.position = cameraTarget.position;
        _virtualCamera.transform.rotation = cameraTarget.rotation;
    }

    /// <summary>
    /// 캐릭터가 없을 때(사망/리스폰 대기) 기지 코어를 비추도록 전환합니다.
    /// </summary>
    public void SetCoreTarget()
    {
        EnsureCamera();
        _currentCharacterTarget = null;

        if (_virtualCamera == null)
        {
            return;
        }

        // 코어 타겟이 없으면 씬에서 자동 탐색
        if (_coreSpectateTarget == null)
        {
            FindCoreTargetFallback();
        }

        if (_coreSpectateTarget != null)
        {
            _virtualCamera.Target.TrackingTarget = _coreSpectateTarget;
            _virtualCamera.Target.LookAtTarget = _coreSpectateTarget;
        }
        else
        {
            // 씬에 코어 오브젝트가 아직 없는 경우 기본 시점 제공
            _virtualCamera.Target.TrackingTarget = null;
            _virtualCamera.Target.LookAtTarget = null;
            _virtualCamera.transform.position = new Vector3(0f, 6f, -12f);
            _virtualCamera.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
        }

        _virtualCamera.Lens.FieldOfView = _defaultFov;
    }

    private void FindCoreTargetFallback()
    {
        // 1. 태그나 이름으로 탐색
        GameObject coreObj = GameObject.Find("ContainmentCore");
        if (coreObj == null) coreObj = GameObject.Find("Core");
        if (coreObj == null) coreObj = GameObject.Find("CoreTarget");
        if (coreObj == null) coreObj = GameObject.FindWithTag("Respawn");

        if (coreObj != null)
        {
            _coreSpectateTarget = coreObj.transform;
            Debug.Log($"[PlayerCameraController] 기지 코어 관전 타겟을 자동으로 찾았습니다: {_coreSpectateTarget.name}");
        }
    }

    public void SetCoreSpectateTarget(Transform target)
    {
        _coreSpectateTarget = target;
        if (_currentCharacterTarget == null)
        {
            SetCoreTarget();
        }
    }

    public void UpdateFov(float targetFov, float speed)
    {
        if (_virtualCamera != null)
        {
            _virtualCamera.Lens.FieldOfView = Mathf.Lerp(
                _virtualCamera.Lens.FieldOfView,
                targetFov,
                Time.deltaTime * speed
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
