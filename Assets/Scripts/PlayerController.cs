using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로컬 플레이어 입력을 받아 1인칭 시점 제어 및 이동시키는 컨트롤러 컴포넌트입니다.
/// 로컬 플레이어(IsOwner)인 경우 Cinemachine 1인칭 카메라를 활성화하고 본인 메시를 숨기며,
/// 원격 플레이어인 경우 카메라를 비활성화합니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    public static PlayerController LocalInstance { get; private set; }

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _gravity = -9.81f;

    [Header("Look / Camera Settings")]
    [SerializeField] private float _mouseSensitivity = 0.1f;
    [SerializeField] private float _topClamp = 80f;
    [SerializeField] private float _bottomClamp = -80f;
    [SerializeField] private Transform _cameraTarget;
    [SerializeField] private CinemachineCamera _firstPersonCamera;

    [Header("Head Settings")]
    [SerializeField] private Transform _headTransform;
    [Tooltip("머리가 아래를 바라볼 때의 각도 상한선 (Pitch 최대값, 바닥에 눈이 꽂히지 않도록 제한. 기본값: 30도)")]
    [SerializeField] private float _headTopClamp = 30f;
    [Tooltip("머리가 위를 바라볼 때의 각도 하한선 (Pitch 최소값. 기본값: -40도)")]
    [SerializeField] private float _headBottomClamp = -40f;
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Input Action References")]
    [Tooltip("비워둘 경우 기본 InputActionAsset(InputSystem_Actions)에서 'Player/Move'를 자동으로 로드합니다.")]
    [SerializeField] private InputActionReference _moveActionReference;
    [Tooltip("비워둘 경우 기본 InputActionAsset(InputSystem_Actions)에서 'Player/Look'을 자동으로 로드합니다.")]
    [SerializeField] private InputActionReference _lookActionReference;

    [Header("Input Asset Fallback")]
    [SerializeField] private InputActionAsset _inputActionsAsset;

    private CharacterController _characterController;
    private ClientNetworkTransform _clientNetworkTransform;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private Vector3 _velocity;
    private float _cameraPitch;
    private bool _isInputEnabled = true;

    private readonly NetworkVariable<float> _networkCameraPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public float CameraPitch => IsOwner ? _cameraPitch : _networkCameraPitch.Value;
    public float HeadTopClamp
    {
        get => _headTopClamp;
        set => _headTopClamp = value;
    }

    public float HeadBottomClamp
    {
        get => _headBottomClamp;
        set => _headBottomClamp = value;
    }

    /// <summary>
    /// 머리 회전 Pitch 각도를 상한선(_headTopClamp)과 하한선(_headBottomClamp) 사이로 제한합니다.
    /// </summary>
    private float GetClampedHeadPitch(float pitch)
    {
        float min = Mathf.Min(_headBottomClamp, _headTopClamp);
        float max = Mathf.Max(_headBottomClamp, _headTopClamp);
        return Mathf.Clamp(pitch, min, max);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        LocalInstance = null;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _clientNetworkTransform = GetComponent<ClientNetworkTransform>();

        // 머리 위 이름표(PlayerNamePlate) 컴포넌트 자동 보장
        if (GetComponent<PlayerNamePlate>() == null)
        {
            gameObject.AddComponent<PlayerNamePlate>();
        }
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
            _characterController.center = new Vector3(0f, 1f, 0f);
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
        }

        // CameraTarget fallback
        if (_cameraTarget == null)
        {
            Transform foundTarget = transform.Find("CameraTarget");
            if (foundTarget != null)
            {
                _cameraTarget = foundTarget;
            }
        }

        // CinemachineCamera fallback
        if (_firstPersonCamera == null)
        {
            _firstPersonCamera = GetComponentInChildren<CinemachineCamera>(true);
        }

        // HeadTransform fallback
        if (_headTransform == null)
        {
            Transform foundHead = transform.Find("Head");
            if (foundHead != null)
            {
                _headTransform = foundHead;
            }
        }

        // BodyRenderer fallback
        if (_bodyRenderer == null)
        {
            Transform body = transform.Find("Body");
            if (body != null)
            {
                _bodyRenderer = body.GetComponent<Renderer>();
            }
            else
            {
                Transform capsule = transform.Find("Capsule");
                if (capsule != null)
                {
                    _bodyRenderer = capsule.GetComponent<Renderer>();
                }
            }
        }
    }

    private void Start()
    {
        // NetworkManager가 없는 오프라인 / 단독 씬 테스트 환경 지원
        if (!IsSpawned)
        {
            LocalInstance = this;
            _isInputEnabled = true;

            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.gameObject.SetActive(true);
                _firstPersonCamera.Priority.Value = 20;
            }

            SetCharacterShadowCastingMode(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _mouseSensitivity = SettingsManager.MouseSensitivity;
            SettingsManager.OnMouseSensitivityChanged += HandleMouseSensitivityChanged;

            InitializeInput();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
            _isInputEnabled = true;

            // 로컬 플레이어: 1인칭 카메라 활성화 및 높은 우선순위 부여
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.gameObject.SetActive(true);
                _firstPersonCamera.Priority.Value = 20;
            }

            // 1인칭 시점: 로컬 캐릭터의 모든 메시를 Shadow Only로 설정하여 시야를 가리지 않으면서 그림자만 생성
            SetCharacterShadowCastingMode(true);

            // 마우스 커서 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 환경 설정에서 마우스 감도 로드 및 이벤트 구독
            _mouseSensitivity = SettingsManager.MouseSensitivity;
            SettingsManager.OnMouseSensitivityChanged += HandleMouseSensitivityChanged;

            InitializeInput();
        }
        else
        {
            // 원격 플레이어: 카메라는 비활성화하여 오디오/렌더링 간섭 방지
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.gameObject.SetActive(false);
            }

            // 원격 플레이어: 정상적으로 모든 부위가 보이도록 ShadowCastingMode.On 적용
            SetCharacterShadowCastingMode(false);

            // 원격 플레이어 초기 머리 각도 적용
            if (_headTransform != null)
            {
                _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(_networkCameraPitch.Value), 0f, 0f);
            }
        }
    }

    /// <summary>
    /// 로컬 플레이어(1인칭)인 경우 모든 자식 렌더러를 ShadowsOnly로 설정하고,
    /// 원격 플레이어인 경우 On으로 설정합니다.
    /// </summary>
    private void SetCharacterShadowCastingMode(bool isFirstPersonOnly)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var shadowMode = isFirstPersonOnly
            ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
            : UnityEngine.Rendering.ShadowCastingMode.On;

        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                r.shadowCastingMode = shadowMode;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            SettingsManager.OnMouseSensitivityChanged -= HandleMouseSensitivityChanged;

            if (LocalInstance == this)
            {
                LocalInstance = null;
            }

            _moveAction?.Disable();
            _lookAction?.Disable();

            // 커서 원상복구
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public override void OnDestroy()
    {
        if (IsOwner || !IsSpawned)
        {
            SettingsManager.OnMouseSensitivityChanged -= HandleMouseSensitivityChanged;

            if (LocalInstance == this)
            {
                LocalInstance = null;
            }
        }

        base.OnDestroy();
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        _mouseSensitivity = sensitivity;
    }

    private void HandleMouseSensitivityChanged(float newSensitivity)
    {
        _mouseSensitivity = newSensitivity;
    }

    private void InitializeInput()
    {
        // Fallback 에셋 로드
        if (_inputActionsAsset == null)
        {
            _inputActionsAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        }

        // Move Action
        if (_moveActionReference != null && _moveActionReference.action != null)
        {
            _moveAction = _moveActionReference.action;
        }
        else if (_inputActionsAsset != null)
        {
            _moveAction = _inputActionsAsset.FindAction("Player/Move");
        }

        if (_moveAction != null)
        {
            _moveAction.Enable();
        }
        else
        {
            Debug.LogError("[PlayerController] 'Player/Move' InputAction을 찾을 수 없습니다.");
        }

        // Look Action
        if (_lookActionReference != null && _lookActionReference.action != null)
        {
            _lookAction = _lookActionReference.action;
        }
        else if (_inputActionsAsset != null)
        {
            _lookAction = _inputActionsAsset.FindAction("Player/Look");
        }

        if (_lookAction != null)
        {
            _lookAction.Enable();
        }
        else
        {
            Debug.LogError("[PlayerController] 'Player/Look' InputAction을 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 메뉴 UI 오픈 등으로 인해 로컬 플레이어 입력(시점 회전, 이동)을 활성화하거나 비활성화합니다.
    /// </summary>
    public void SetInputEnabled(bool isEnabled)
    {
        _isInputEnabled = isEnabled;
    }

    private void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            UpdateRemoteHead();
            return;
        }

        if (_characterController == null || !_characterController.enabled || !_isInputEnabled)
        {
            return;
        }

        HandleLook();
        HandleMovement();
    }

    /// <summary>
    /// 원격 플레이어의 머리 상하 각도를 네트워크 동기화 값으로 부드럽게 보간합니다.
    /// </summary>
    private void UpdateRemoteHead()
    {
        if (_headTransform != null)
        {
            Quaternion targetRotation = Quaternion.Euler(GetClampedHeadPitch(_networkCameraPitch.Value), 0f, 0f);
            _headTransform.localRotation = Quaternion.Slerp(_headTransform.localRotation, targetRotation, Time.deltaTime * 20f);
        }
    }

    private void HandleLook()
    {
        Vector2 lookVector = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

        // 마우스 감도 적용
        float mouseX = lookVector.x * _mouseSensitivity;
        float mouseY = lookVector.y * _mouseSensitivity;

        // 1. 플레이어 몸체 좌우 회전 (Yaw)
        transform.Rotate(Vector3.up * mouseX);

        // 2. 카메라 및 머리 상하 회전 (Pitch 제한)
        _cameraPitch -= mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, _bottomClamp, _topClamp);

        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }

        if (_headTransform != null)
        {
            _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(_cameraPitch), 0f, 0f);
        }

        if (IsSpawned && IsOwner && Mathf.Abs(_networkCameraPitch.Value - _cameraPitch) > 0.05f)
        {
            _networkCameraPitch.Value = _cameraPitch;
        }
    }

    private void HandleMovement()
    {
        Vector2 inputVector = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

        // 플레이어 캐릭터가 바라보는 방향 기준(Local)으로 앞/뒤, 좌/우 이동 벡터 계산
        Vector3 moveDirection = (transform.forward * inputVector.y) + (transform.right * inputVector.x);

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        // 수평 이동
        Vector3 motion = moveDirection * (_moveSpeed * Time.deltaTime);

        // 지면 접지 및 중력 계산
        if (_characterController.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f;
        }

        _velocity.y += _gravity * Time.deltaTime;
        motion.y = _velocity.y * Time.deltaTime;

        _characterController.Move(motion);
    }

    /// <summary>
    /// 지정된 위치, 회전 및 카메라 상하 각도로 캐릭터를 즉시 텔레포트합니다.
    /// CharacterController와 ClientNetworkTransform을 모두 갱신하여 클라이언트 권한 동기화를 유지합니다.
    /// </summary>
    public void Teleport(Vector3 targetPosition, Quaternion targetRotation, float cameraPitch = 0f)
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        _cameraPitch = Mathf.Clamp(cameraPitch, _bottomClamp, _topClamp);
        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }

        if (_headTransform != null)
        {
            _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(_cameraPitch), 0f, 0f);
        }

        if (IsSpawned && IsOwner)
        {
            _networkCameraPitch.Value = _cameraPitch;
        }

        if (_clientNetworkTransform != null)
        {
            _clientNetworkTransform.Teleport(targetPosition, targetRotation, transform.localScale);
        }

        if (_characterController != null)
        {
            _characterController.enabled = true;
        }
    }

    /// <summary>
    /// 서버에서 호출하여 소유자 클라이언트에게 텔레포트를 수행하도록 지시합니다.
    /// </summary>
    [ClientRpc]
    public void TeleportClientRpc(Vector3 targetPosition, Quaternion targetRotation, float cameraPitch, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
        {
            return;
        }

        Teleport(targetPosition, targetRotation, cameraPitch);
        Debug.Log($"[PlayerController] 소유 클라이언트에서 텔레포트 수행 완료: 위치 {targetPosition}, 각도 {targetRotation.eulerAngles}, 카메라 {cameraPitch}");
    }
}
