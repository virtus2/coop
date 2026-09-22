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

    [Header("Sprint & Stamina Settings")]
    [Tooltip("달리기 속도 배율 (기본 이동 속도 대비, 기본값: 1.75배)")]
    [SerializeField] private float _sprintSpeedMultiplier = 1.75f;
    [Tooltip("달리기 최대 속도까지 부드럽게 가속되는 데 걸리는 시간(초)")]
    [SerializeField] private float _accelerationTime = 0.4f;
    [Tooltip("걷기 속도로 부드럽게 감속되는 데 걸리는 시간(초)")]
    [SerializeField] private float _decelerationTime = 0.3f;
    [Tooltip("달릴 수 있는 최대 스태미나 시간(초, 기본값: 7초)")]
    [SerializeField] private float _maxStamina = 7f;
    [Tooltip("달리기를 멈춘 후 스태미나 회복이 시작될 때까지의 대기 시간(초)")]
    [SerializeField] private float _staminaRegenDelay = 1.5f;
    [Tooltip("초당 스태미나 회복량 (약 4초에 걸쳐 완충)")]
    [SerializeField] private float _staminaRegenRate = 1.75f;
    [Tooltip("탈진 후 다시 달리기 위해 필요한 최소 스태미나")]
    [SerializeField] private float _exhaustionRecoveryThreshold = 1.4f;

    [Header("Camera FOV Settings")]
    [Tooltip("달릴 때 증가할 카메라 FOV 오프셋")]
    [SerializeField] private float _sprintFovOffset = 8f;
    [Tooltip("카메라 FOV 보간 속도")]
    [SerializeField] private float _fovTransitionSpeed = 6f;

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
    [Tooltip("비워둘 경우 기본 InputActionAsset(InputSystem_Actions)에서 'Player/Sprint'를 자동으로 로드합니다.")]
    [SerializeField] private InputActionReference _sprintActionReference;

    [Header("Input Asset Fallback")]
    [SerializeField] private InputActionAsset _inputActionsAsset;

    private CharacterController _characterController;
    private ClientNetworkTransform _clientNetworkTransform;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _sprintAction;
    private Vector3 _velocity;
    private float _cameraPitch;
    private bool _isInputEnabled = true;

    // 달리기 및 스태미나 제어 변수
    private float _currentSpeed;
    private float _currentStamina;
    private float _staminaDelayTimer;
    private bool _isExhausted;
    private bool _isSprinting;

    // 카메라 FOV 제어 변수
    private float _baseFov;
    private bool _isBaseFovCached;

    public bool IsSprinting => _isSprinting;
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;

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

        // 플레이어 캐릭터 및 하위 콜라이더들의 레이어를 'Player' 레이어로 보장 (아이템 물리 충돌 무시 매트릭스 적용)
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            SetLayerRecursively(gameObject, playerLayer);
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

        _currentSpeed = _moveSpeed;
        _currentStamina = _maxStamina;

        if (_firstPersonCamera != null)
        {
            _baseFov = _firstPersonCamera.Lens.FieldOfView;
            _isBaseFovCached = true;
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
            _sprintAction?.Disable();

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

        // Sprint Action
        if (_sprintActionReference != null && _sprintActionReference.action != null)
        {
            _sprintAction = _sprintActionReference.action;
        }
        else if (_inputActionsAsset != null)
        {
            _sprintAction = _inputActionsAsset.FindAction("Player/Sprint");
        }

        if (_sprintAction != null)
        {
            _sprintAction.Enable();
        }
        else
        {
            Debug.LogWarning("[PlayerController] 'Player/Sprint' InputAction을 찾을 수 없습니다.");
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
        UpdateStamina();
        UpdateCameraFov();
    }

    /// <summary>
    /// 원격 플레이어의 머리 및 카메라 타겟(손 HoldPoint 포함) 상하 각도를 네트워크 동기화 값으로 부드럽게 보간합니다.
    /// </summary>
    private void UpdateRemoteHead()
    {
        Quaternion headTargetRotation = Quaternion.Euler(GetClampedHeadPitch(_networkCameraPitch.Value), 0f, 0f);
        if (_headTransform != null)
        {
            _headTransform.localRotation = Quaternion.Slerp(_headTransform.localRotation, headTargetRotation, Time.deltaTime * 20f);
        }

        if (_cameraTarget != null)
        {
            Quaternion cameraTargetRotation = Quaternion.Euler(_networkCameraPitch.Value, 0f, 0f);
            _cameraTarget.localRotation = Quaternion.Slerp(_cameraTarget.localRotation, cameraTargetRotation, Time.deltaTime * 20f);
        }
    }

    // --- 반동(Recoil) 및 자동 복구 상태 변수 ---
    private float _recoilPitchOffset;
    private float _recoilYawOffset;
    private float _recoilRecoverySpeed = 10f;

    /// <summary>
    /// 총기 격발 시 화면 반동을 부여합니다.
    /// </summary>
    /// <param name="pitchKick">상단으로 튕길 각도</param>
    /// <param name="yawKick">좌우 랜덤 튕김 범위</param>
    /// <param name="recoverySpeed">원래 에임으로 복귀하는 속도</param>
    public void ApplyRecoil(float pitchKick, float yawKick, float recoverySpeed = 10f)
    {
        _recoilPitchOffset += pitchKick;
        _recoilYawOffset += UnityEngine.Random.Range(-yawKick, yawKick);
        _recoilRecoverySpeed = recoverySpeed;
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

        // 3. 반동 복구 (Recoil Recovery - 부드럽게 0으로 수렴)
        if (_recoilPitchOffset > 0f)
        {
            _recoilPitchOffset = Mathf.MoveTowards(_recoilPitchOffset, 0f, _recoilRecoverySpeed * Time.deltaTime);
        }
        if (Mathf.Abs(_recoilYawOffset) > 0f)
        {
            _recoilYawOffset = Mathf.MoveTowards(_recoilYawOffset, 0f, _recoilRecoverySpeed * Time.deltaTime);
        }

        // 반동 오프셋이 적용된 실제 카메라 조준 각도
        float finalCameraPitch = Mathf.Clamp(_cameraPitch - _recoilPitchOffset, _bottomClamp, _topClamp);

        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(finalCameraPitch, _recoilYawOffset, 0f);
        }

        if (_headTransform != null)
        {
            _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(finalCameraPitch), 0f, 0f);
        }

        if (IsSpawned && IsOwner && Mathf.Abs(_networkCameraPitch.Value - _cameraPitch) > 0.05f)
        {
            _networkCameraPitch.Value = _cameraPitch;
        }
    }

    // 사격 페널티 및 스프린트 중단 제어 변수
    private float _sprintInterruptTimer;
    private float _shootingPenaltyTimer;
    private float _shootingPenaltyMultiplier = 1.0f;

    public bool IsSprintPressed => _sprintAction != null && _sprintAction.IsPressed();

    /// <summary>
    /// 사격 또는 재장전 시 전력 질주(달리기)를 즉시 중단합니다.
    /// </summary>
    public void CancelSprint(float blockDuration = 0.15f)
    {
        _isSprinting = false;
        _sprintInterruptTimer = Mathf.Max(_sprintInterruptTimer, blockDuration);
    }

    /// <summary>
    /// 사격 중 이동 속도 감속 페널티를 부여합니다.
    /// </summary>
    public void ApplyShootingPenalty(float multiplier, float duration)
    {
        _shootingPenaltyMultiplier = multiplier;
        _shootingPenaltyTimer = Mathf.Max(_shootingPenaltyTimer, duration);
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

        if (_sprintInterruptTimer > 0f)
        {
            _sprintInterruptTimer -= Time.deltaTime;
        }

        if (_shootingPenaltyTimer > 0f)
        {
            _shootingPenaltyTimer -= Time.deltaTime;
        }

        // 1. 달리기 조건 체크: 전진 입력(y > 0.1f) + Sprint 키 홀드 + 탈진 상태 아님 + 스태미나 잔여 + 사격 인터럽트 없음
        bool isMovingForward = inputVector.y > 0.1f;
        bool isSprintPressed = _sprintAction != null && _sprintAction.IsPressed();
        bool canSprint = isMovingForward && isSprintPressed && !_isExhausted && _currentStamina > 0f && inputVector.sqrMagnitude > 0.01f && _sprintInterruptTimer <= 0f;

        _isSprinting = canSprint;

        // 2. 목표 속도 및 가속/감속 보간 (사격 페널티 배율 반영)
        float currentBaseSpeed = _moveSpeed * (_shootingPenaltyTimer > 0f ? _shootingPenaltyMultiplier : 1.0f);
        float maxSprintSpeed = currentBaseSpeed * _sprintSpeedMultiplier;
        float targetSpeed = _isSprinting ? maxSprintSpeed : currentBaseSpeed;
        float speedDelta = maxSprintSpeed - _moveSpeed;
        float accelRate = (_currentSpeed < targetSpeed)
            ? (speedDelta / Mathf.Max(0.01f, _accelerationTime))
            : (speedDelta / Mathf.Max(0.01f, _decelerationTime));

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accelRate * Time.deltaTime);

        // 3. 수평 이동
        Vector3 motion = moveDirection * (_currentSpeed * Time.deltaTime);

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
    /// 달리기 중 스태미나 소모(7초) 및 정지 후 1.5초 대기 후 회복을 처리합니다.
    /// </summary>
    private void UpdateStamina()
    {
        if (_isSprinting)
        {
            // 달리는 중: 스태미나 초당 1f 소모 (기본 7초 동안 지속 가능)
            _currentStamina -= Time.deltaTime;
            _staminaDelayTimer = 0f;

            if (_currentStamina <= 0f)
            {
                _currentStamina = 0f;
                _isExhausted = true;
                _isSprinting = false;
            }
        }
        else
        {
            // 달리지 않는 중: 1.5초 대기 후 서서히 회복
            if (_staminaDelayTimer < _staminaRegenDelay)
            {
                _staminaDelayTimer += Time.deltaTime;
            }
            else
            {
                if (_currentStamina < _maxStamina)
                {
                    _currentStamina = Mathf.MoveTowards(_currentStamina, _maxStamina, _staminaRegenRate * Time.deltaTime);
                }

                // 탈진 상태 해제 (최소 임계치 이상 회복 시)
                if (_isExhausted && _currentStamina >= _exhaustionRecoveryThreshold)
                {
                    _isExhausted = false;
                }
            }
        }
    }

    /// <summary>
    /// 달리기 상태에 따라 1인칭 카메라의 FOV를 부드럽게 조정합니다.
    /// </summary>
    private void UpdateCameraFov()
    {
        if (_firstPersonCamera == null)
        {
            return;
        }

        if (!_isBaseFovCached)
        {
            _baseFov = _firstPersonCamera.Lens.FieldOfView;
            _isBaseFovCached = true;
        }

        float targetFov = _isSprinting ? (_baseFov + _sprintFovOffset) : _baseFov;
        _firstPersonCamera.Lens.FieldOfView = Mathf.Lerp(
            _firstPersonCamera.Lens.FieldOfView,
            targetFov,
            Time.deltaTime * _fovTransitionSpeed
        );
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

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
