using System;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 월드 상에 시각적/물리적으로 존재하는 플레이어 캐릭터(아바타) 컴포넌트입니다.
/// CharacterController 기반으로 부드러운 이동, 점프, 스태미나, 1인칭/3인칭 머리 회전 및 피치 동기화를 처리하며,
/// OnControllerColliderHit을 통해 서버 전담 물리 객체 밀기 및 질량 기반 이동 저항(감속)을 수행합니다.
/// 네트워크 상의 플레이어 세션(NetworkPlayer)에 의해 빙의(Possess)되어 제어됩니다.
/// </summary>
public class PlayerCharacter : NetworkBehaviour
{
    public static PlayerCharacter LocalInstance { get; protected set; }

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _accelerationRate = 25f;
    [SerializeField] private float _jumpForce = 5.5f;
    [SerializeField] private float _airControl = 0.35f;
    [SerializeField] private float _mass = 70f;
    [SerializeField] private float _slopeLimit = 55f;
    [SerializeField] private float _fallGravityMultiplier = 1.5f;

    [Header("Ground Check Settings")]
    [SerializeField] private float _groundCheckRadius = 0.35f;
    [SerializeField] private float _groundCheckDistance = 0.25f;
    [SerializeField] private LayerMask _groundLayers = ~0;

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

    [Header("Physics Push Settings")]
    [Tooltip("플레이어가 밀어낼 때 가해지는 기본 충격량")]
    [SerializeField] private float _pushPower = 3.0f;
    [Tooltip("완전 감속(최대 70% 감속)에 도달하는 물리 객체의 최대 기준 질량(kg)")]
    [SerializeField] private float _maxPushableMass = 100f;

    [Header("Camera FOV Settings")]
    [Tooltip("달릴 때 증가할 카메라 FOV 오프셋")]
    [SerializeField] private float _sprintFovOffset = 8f;
    [Tooltip("카메라 FOV 보간 속도")]
    [SerializeField] private float _fovTransitionSpeed = 6f;

    [Header("Look / Camera Settings")]
    [SerializeField] private float _mouseSensitivity = 0.1f;
    [SerializeField] private float _topClamp = 80f;
    [SerializeField] private float _bottomClamp = -80f;
    [SerializeField] protected Transform _cameraTarget;
    [SerializeField] protected CinemachineCamera _firstPersonCamera;

    [Header("Recoil Settings")]
    [Tooltip("기본 반동 튕김 속도(Snappiness). 높을수록 빠르고 경쾌하게 튕김")]
    [SerializeField] private float _defaultRecoilSnappiness = 25f;

    // 런타임 반동 상태 변수
    private Vector2 _targetRecoil = Vector2.zero;
    private Vector2 _currentRecoil = Vector2.zero;
    private float _activeRecoilRecoverySpeed = 10f;
    private float _activeRecoilSnappiness = 25f;

    [Header("Head Settings")]
    [SerializeField] protected Transform _headTransform;
    [Tooltip("머리가 아래를 바라볼 때의 각도 상한선 (Pitch 최대값)")]
    [SerializeField] private float _headTopClamp = 30f;
    [Tooltip("머리가 위를 바라볼 때의 각도 하한선 (Pitch 최소값)")]
    [SerializeField] private float _headBottomClamp = -40f;
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Input Action References")]
    [SerializeField] private InputActionReference _moveActionReference;
    [SerializeField] private InputActionReference _lookActionReference;
    [SerializeField] private InputActionReference _sprintActionReference;
    [SerializeField] private InputActionReference _jumpActionReference;

    [Header("Input Asset Fallback")]
    [SerializeField] private InputActionAsset _inputActionsAsset;

    protected CharacterController _characterController;
    protected Rigidbody _rigidbody;
    protected CapsuleCollider _capsuleCollider;
    protected ClientNetworkTransform _clientNetworkTransform;
    protected Animator _animator;
    protected PlayerCharacterIK _characterIK;
    protected InputAction _moveAction;
    protected InputAction _lookAction;
    protected InputAction _sprintAction;
    protected InputAction _jumpAction;
    protected float _cameraPitch;
    protected bool _isInputEnabled = true;

    // 이동 및 수직 속도
    protected bool _isGrounded;
    private float _verticalVelocity;
    private bool _isJumpQueued;
    private float _jumpCooldownTimer;
    private float _knockbackTimer;
    private Vector3 _knockbackVelocity;

    // 물리 밀기 감속 변수
    private float _pushSpeedMultiplier = 1f;
    private float _pushPenaltyTimer;

    public CharacterController CharacterController => _characterController;
    public Rigidbody Rigidbody => _rigidbody;
    public CapsuleCollider CapsuleCollider => _capsuleCollider;
    public bool IsGrounded => _isGrounded;

    // 달리기 및 스태미나 제어 변수
    private float _currentSpeed;
    private float _currentStamina;
    private float _staminaDelayTimer;
    private bool _isExhausted;
    private bool _isSprinting;

    // 카메라 FOV 제어 변수
    private float _baseFov;
    private bool _isBaseFovCached;

    // 총기/근접 공격 이동 속도 페널티 변수
    private float _speedPenaltyMultiplier = 1f;
    private float _speedPenaltyDuration = 0f;

    // 달리기 강제 취소(스프린트 락) 쿨다운
    private float _sprintLockDuration = 0f;

    private readonly NetworkVariable<bool> _networkIsSprinting = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<float> _networkCameraPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public bool IsSprinting => (!IsSpawned || IsOwner) ? _isSprinting : _networkIsSprinting.Value;
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;
    public float CameraPitch => IsOwner ? Mathf.Clamp(_cameraPitch - _currentRecoil.x, _bottomClamp, _topClamp) : _networkCameraPitch.Value;
    public float BaseCameraPitch => _cameraPitch;
    public Vector2 CurrentRecoil => _currentRecoil;
    public Vector2 TargetRecoil => _targetRecoil;
    public Transform CameraTarget => _cameraTarget;

    // 소유 세션 플레이어 참조
    public NetworkPlayer OwningPlayer { get; private set; }
    public PlayerInventory Inventory => OwningPlayer != null ? OwningPlayer.Inventory : (IsOwner ? PlayerInventory.LocalInstance : null);

    public event Action<PlayerInventory> OnInventoryBound;

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

    protected virtual void Awake()
    {
        // 1. CharacterController 초기화 (무한 질량 Kinematic 벽체 역할)
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
        }
        _characterController.height = 2.0f;
        _characterController.radius = 0.45f;
        _characterController.center = new Vector3(0f, 1f, 0f);
        _characterController.stepOffset = 0.3f;
        _characterController.slopeLimit = _slopeLimit;
        _characterController.minMoveDistance = 0.001f;

        // 2. 외부 호환용 Rigidbody 설정 (PhysX 직접 충돌 간섭 방지를 위해 isKinematic 설정)
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }

        // 3. 중복 캡슐 콜라이더 비활성화
        _capsuleCollider = GetComponent<CapsuleCollider>();
        if (_capsuleCollider != null)
        {
            _capsuleCollider.enabled = false;
        }

        _clientNetworkTransform = GetComponent<ClientNetworkTransform>();

        if (GetComponent<PlayerNamePlate>() == null)
        {
            gameObject.AddComponent<PlayerNamePlate>();
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            SetLayerRecursively(gameObject, playerLayer);
        }

        if (_cameraTarget == null)
        {
            Transform foundTarget = transform.Find("CameraTarget");
            if (foundTarget != null)
            {
                _cameraTarget = foundTarget;
            }
        }

        if (_firstPersonCamera == null)
        {
            _firstPersonCamera = GetComponentInChildren<CinemachineCamera>(true);
        }

        if (_headTransform == null)
        {
            Transform foundHead = transform.Find("Head");
            if (foundHead != null)
            {
                _headTransform = foundHead;
            }
        }

        if (_bodyRenderer == null)
        {
            _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
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

        _animator = GetComponent<Animator>();
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
        }
        _characterIK = GetComponent<PlayerCharacterIK>();

        _currentSpeed = _moveSpeed;
        _currentStamina = _maxStamina;

        if (_firstPersonCamera != null)
        {
            _baseFov = _firstPersonCamera.Lens.FieldOfView;
            _isBaseFovCached = true;
        }
    }

    protected virtual void Start()
    {
        if (!IsSpawned)
        {
            LocalInstance = this;
            _isInputEnabled = true;

            BindCamera();
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

            BindCamera();
            SetCharacterShadowCastingMode(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _mouseSensitivity = SettingsManager.MouseSensitivity;
            SettingsManager.OnMouseSensitivityChanged += HandleMouseSensitivityChanged;

            InitializeInput();

            if (Inventory != null)
            {
                OnInventoryBound?.Invoke(Inventory);
            }
        }
        else
        {
            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.gameObject.SetActive(false);
            }

            SetCharacterShadowCastingMode(false);

            if (_headTransform != null)
            {
                _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(_networkCameraPitch.Value), 0f, 0f);
            }
        }
    }

    private void BindCamera()
    {
        if (_firstPersonCamera != null)
        {
            _firstPersonCamera.Target.TrackingTarget = null;
            _firstPersonCamera.Target.LookAtTarget = null;
            _firstPersonCamera.transform.localPosition = Vector3.zero;
            _firstPersonCamera.transform.localRotation = Quaternion.identity;
            _firstPersonCamera.gameObject.SetActive(true);
            _firstPersonCamera.Priority.Value = 20;
        }

        if (PlayerCameraController.Instance != null && _cameraTarget != null)
        {
            PlayerCameraController.Instance.SetCharacterTarget(_cameraTarget);
        }
    }

    public void SetOwningPlayer(NetworkPlayer player)
    {
        OwningPlayer = player;
        if (Inventory != null)
        {
            OnInventoryBound?.Invoke(Inventory);
        }
    }

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
                if (r.gameObject.name.StartsWith("HeldItemVisual"))
                {
                    continue;
                }
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
            _jumpAction?.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (PlayerCameraController.Instance != null)
            {
                PlayerCameraController.Instance.SetCoreTarget();
            }
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

            if (PlayerCameraController.Instance != null)
            {
                PlayerCameraController.Instance.SetCoreTarget();
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

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void InitializeInput()
    {
        if (_moveActionReference != null)
        {
            _moveAction = _moveActionReference.action;
        }
        else
        {
            _moveAction = FindAction("Player/Move");
        }

        if (_lookActionReference != null)
        {
            _lookAction = _lookActionReference.action;
        }
        else
        {
            _lookAction = FindAction("Player/Look");
        }

        if (_sprintActionReference != null)
        {
            _sprintAction = _sprintActionReference.action;
        }
        else
        {
            _sprintAction = FindAction("Player/Sprint");
        }

        if (_jumpActionReference != null)
        {
            _jumpAction = _jumpActionReference.action;
        }
        else
        {
            _jumpAction = FindAction("Player/Jump");
        }

        _moveAction?.Enable();
        _lookAction?.Enable();
        _sprintAction?.Enable();
        _jumpAction?.Enable();
    }

    private InputAction FindAction(string actionPath)
    {
        if (_inputActionsAsset != null)
        {
            var action = _inputActionsAsset.FindAction(actionPath);
            if (action != null)
            {
                return action;
            }
        }

        var defaultAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        if (defaultAsset != null)
        {
            var action = defaultAsset.FindAction(actionPath);
            if (action != null)
            {
                return action;
            }
        }

        return null;
    }

    protected virtual void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            if (_headTransform != null)
            {
                _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(_networkCameraPitch.Value), 0f, 0f);
            }
            return;
        }

        HandleLook();
        UpdateRecoil();
        HandleSprintAndStamina();
        HandleJumpInput();
        HandleCharacterMovement();
        UpdateCameraFov();
    }

    private void HandleJumpInput()
    {
        if (_jumpCooldownTimer > 0f)
        {
            _jumpCooldownTimer -= Time.deltaTime;
        }

        if (!_isInputEnabled || _jumpAction == null)
        {
            return;
        }

        if (_jumpAction.WasPressedThisFrame() && _isGrounded && _jumpCooldownTimer <= 0f)
        {
            _isJumpQueued = true;
        }
    }

    private void HandleCharacterMovement()
    {
        if (_characterController == null || !_characterController.enabled)
        {
            return;
        }

        // 물리 객체 충돌 감속 타이머 갱신
        if (_pushPenaltyTimer > 0f)
        {
            _pushPenaltyTimer -= Time.deltaTime;
            if (_pushPenaltyTimer <= 0f)
            {
                _pushSpeedMultiplier = 1f;
            }
        }

        Vector2 moveInput = Vector2.zero;
        if (_isInputEnabled && _moveAction != null)
        {
            moveInput = _moveAction.ReadValue<Vector2>();
        }

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        float effectiveSpeed = _currentSpeed * _speedPenaltyMultiplier * _pushSpeedMultiplier;

        // 1. 지면 및 수직 속도 처리
        _isGrounded = _characterController.isGrounded;
        if (_isGrounded)
        {
            if (_verticalVelocity < 0f)
            {
                _verticalVelocity = -2f; // 지면에 안정적으로 밀착되도록 미세한 하향 속도 유지
            }

            if (_isJumpQueued && _jumpCooldownTimer <= 0f)
            {
                _isJumpQueued = false;
                _jumpCooldownTimer = 0.25f;
                _verticalVelocity = _jumpForce;
                _isGrounded = false;
            }
        }
        else
        {
            float gravity = Physics.gravity.y;
            if (_verticalVelocity < 0f && _fallGravityMultiplier > 1f)
            {
                gravity *= _fallGravityMultiplier;
            }
            _verticalVelocity += gravity * Time.deltaTime;
        }

        // 2. 최종 이동 벡터 계산 및 실행
        Vector3 motion = (move * effectiveSpeed) + (Vector3.up * _verticalVelocity);

        // 3. 넉백 속도 합성
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= Time.deltaTime;
            motion += _knockbackVelocity;
            _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, Time.deltaTime * 5f);
        }

        _characterController.Move(motion * Time.deltaTime);

        // 애니메이터 파라미터 업데이트
        if (_animator != null)
        {
            _animator.SetFloat("MoveX", moveInput.x, 0.1f, Time.deltaTime);
            _animator.SetFloat("MoveY", moveInput.y, 0.1f, Time.deltaTime);
            _animator.SetBool("IsSprinting", _isSprinting);
            _animator.SetBool("IsMoving", moveInput.sqrMagnitude > 0.01f);
            _animator.SetFloat("Speed", moveInput.sqrMagnitude > 0.01f ? effectiveSpeed : 0f);
        }
    }

    /// <summary>
    /// CharacterController가 이동하면서 다른 콜라이더(물리 객체)와 충돌했을 때 실행됩니다.
    /// 서버 측에서 물리 객체에 추진력을 가하고, 질량에 비례하여 플레이어 이동 속도를 감속시킵니다.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic)
        {
            return;
        }

        // 발밑 바닥 충돌은 제외 (밟고 서 있는 바닥 물체 밀기 방지)
        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        // 1. 호스트(서버) 측: 물리 객체에 힘 가하기
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer || !IsSpawned)
        {
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (pushDir.sqrMagnitude < 0.001f)
            {
                pushDir = transform.forward;
            }

            body.AddForceAtPosition(pushDir * _pushPower, hit.point, ForceMode.Impulse);

            if (hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                pickable.WakeUpPhysics();
            }
        }

        // 2. 플레이어 이동 속도 저항 (질량 기반 자동 계산)
        if (IsOwner || !IsSpawned)
        {
            float propMass = body.mass;
            float resistance = Mathf.Clamp01(propMass / _maxPushableMass);
            _pushSpeedMultiplier = Mathf.Max(0.2f, 1f - (resistance * 0.7f));
            _pushPenaltyTimer = 0.15f;
        }
    }

    private void HandleLook()
    {
        if (!_isInputEnabled || _lookAction == null)
        {
            return;
        }

        Vector2 lookInput = _lookAction.ReadValue<Vector2>();

        float mouseX = lookInput.x * _mouseSensitivity;
        float mouseY = lookInput.y * _mouseSensitivity;

        // 마우스 상하(Pitch) 조작 및 반동 상쇄(Recoil Compensation) 처리
        if (mouseY < 0f && _targetRecoil.x > 0.001f)
        {
            // 사용자가 마우스를 아래로 당겨 반동을 제어(Pull-down)하는 경우
            float pullDown = -mouseY;
            if (pullDown <= _targetRecoil.x)
            {
                // 반동 목표치 및 현재 반동에서 즉각 차감하여 에임 제어 반응성 100% 보장
                _targetRecoil.x -= pullDown;
                _currentRecoil.x = Mathf.Max(0f, _currentRecoil.x - pullDown);
            }
            else
            {
                // 반동을 모두 상쇄하고 남은 초과분만 기본 시선 피치에 반영
                float excess = pullDown - _targetRecoil.x;
                _targetRecoil.x = 0f;
                _currentRecoil.x = 0f;
                _cameraPitch += excess;
            }
        }
        else
        {
            _cameraPitch -= mouseY;
        }

        _cameraPitch = Mathf.Clamp(_cameraPitch, _bottomClamp, _topClamp);

        transform.Rotate(Vector3.up * mouseX);
    }

    /// <summary>
    /// 사격 등으로 누적된 반동을 스무스하게 튕기게 하고(Snappy Kick),
    /// 원래 조준점 위치로 부드럽게 감쇄 및 복구(Smooth Ease-out Recovery)합니다.
    /// </summary>
    private void UpdateRecoil()
    {
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            // 1. 목표 반동 감쇄 (부드러운 복구 / Return to Center)
            _targetRecoil.x = Mathf.Lerp(_targetRecoil.x, 0f, dt * _activeRecoilRecoverySpeed);
            _targetRecoil.y = Mathf.Lerp(_targetRecoil.y, 0f, dt * _activeRecoilRecoverySpeed);

            if (_targetRecoil.sqrMagnitude < 0.00001f)
            {
                _targetRecoil = Vector2.zero;
            }

            // 2. 현재 반동 보간 (스무스 킥 & 복구 추적)
            _currentRecoil.x = Mathf.Lerp(_currentRecoil.x, _targetRecoil.x, dt * _activeRecoilSnappiness);
            _currentRecoil.y = Mathf.Lerp(_currentRecoil.y, _targetRecoil.y, dt * _activeRecoilSnappiness);

            if (_currentRecoil.sqrMagnitude < 0.00001f && _targetRecoil == Vector2.zero)
            {
                _currentRecoil = Vector2.zero;
            }
        }

        // 3. 최종 시선 회전 합성 (기본 피치 - 반동 피치, 반동 요)
        float totalPitch = Mathf.Clamp(_cameraPitch - _currentRecoil.x, _bottomClamp, _topClamp);
        float totalYaw = _currentRecoil.y;

        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(totalPitch, totalYaw, 0f);
        }

        if (_headTransform != null)
        {
            _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(totalPitch), 0f, 0f);
        }

        if (IsSpawned && IsOwner)
        {
            _networkCameraPitch.Value = totalPitch;
        }
    }

    private void HandleSprintAndStamina()
    {
        if (_sprintLockDuration > 0f)
        {
            _sprintLockDuration -= Time.deltaTime;
        }

        if (_speedPenaltyDuration > 0f)
        {
            _speedPenaltyDuration -= Time.deltaTime;
            if (_speedPenaltyDuration <= 0f)
            {
                _speedPenaltyMultiplier = 1f;
            }
        }

        Vector2 moveInput = Vector2.zero;
        if (_isInputEnabled && _moveAction != null)
        {
            moveInput = _moveAction.ReadValue<Vector2>();
        }

        bool isMovingForward = moveInput.y > 0.1f;
        bool sprintKeyPressed = false;
        if (_isInputEnabled && _sprintAction != null)
        {
            sprintKeyPressed = _sprintAction.IsPressed();
        }

        bool canSprint = isMovingForward && sprintKeyPressed && !_isExhausted && _currentStamina > 0f && _sprintLockDuration <= 0f;

        if (canSprint)
        {
            _isSprinting = true;
            _staminaDelayTimer = 0f;

            _currentStamina -= Time.deltaTime;
            if (_currentStamina <= 0f)
            {
                _currentStamina = 0f;
                _isExhausted = true;
                _isSprinting = false;
            }

            float targetSprintSpeed = _moveSpeed * _sprintSpeedMultiplier;
            float accelRate = (targetSprintSpeed - _moveSpeed) / Mathf.Max(_accelerationTime, 0.01f);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSprintSpeed, accelRate * Time.deltaTime);
        }
        else
        {
            _isSprinting = false;

            float decelRate = (_moveSpeed * _sprintSpeedMultiplier - _moveSpeed) / Mathf.Max(_decelerationTime, 0.01f);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _moveSpeed, decelRate * Time.deltaTime);

            if (_staminaDelayTimer < _staminaRegenDelay)
            {
                _staminaDelayTimer += Time.deltaTime;
            }
            else
            {
                if (_currentStamina < _maxStamina)
                {
                    _currentStamina += _staminaRegenRate * Time.deltaTime;
                    if (_currentStamina > _maxStamina)
                    {
                        _currentStamina = _maxStamina;
                    }
                }

                if (_isExhausted && _currentStamina >= _exhaustionRecoveryThreshold)
                {
                    _isExhausted = false;
                }
            }
        }

        if (IsSpawned && IsOwner)
        {
            if (_networkIsSprinting.Value != _isSprinting)
            {
                _networkIsSprinting.Value = _isSprinting;
            }
        }
    }

    private void UpdateCameraFov()
    {
        if (_firstPersonCamera != null)
        {
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
        else if (PlayerCameraController.Instance != null)
        {
            float targetFov = _isSprinting ? (60f + _sprintFovOffset) : 60f;
            PlayerCameraController.Instance.UpdateFov(targetFov, _fovTransitionSpeed);
        }
    }

    public void ApplyShootingPenalty(float multiplier, float duration)
    {
        _speedPenaltyMultiplier = multiplier;
        _speedPenaltyDuration = Mathf.Max(_speedPenaltyDuration, duration);
    }

    public void CancelSprint(float lockDuration = 0.2f)
    {
        _isSprinting = false;
        _sprintLockDuration = Mathf.Max(_sprintLockDuration, lockDuration);
        _currentSpeed = _moveSpeed;
    }

    /// <summary>
    /// 사격 시 발생하는 카메라 반동을 적용합니다.
    /// pitchOffset: 상향 각도, yawOffset: 좌우 흔들림 최대 범위, recoverySpeed: 원래 조준점으로 복귀하는 속도, snappiness: 반동 튕김 반응 속도.
    /// </summary>
    public void ApplyRecoil(float pitchOffset, float yawOffset, float recoverySpeed = 10f, float snappiness = 25f)
    {
        if (!IsOwner && IsSpawned)
        {
            return;
        }

        _activeRecoilRecoverySpeed = Mathf.Max(1f, recoverySpeed);
        _activeRecoilSnappiness = Mathf.Max(1f, snappiness > 0.01f ? snappiness : _defaultRecoilSnappiness);

        // 상향 피치 반동 누적 (상한선 25도)
        _targetRecoil.x = Mathf.Clamp(_targetRecoil.x + pitchOffset, 0f, 25f);

        // 좌우 랜덤 요 반동 분산 (-15도 ~ 15도)
        float randomYaw = UnityEngine.Random.Range(-yawOffset, yawOffset);
        _targetRecoil.y = Mathf.Clamp(_targetRecoil.y + randomYaw, -15f, 15f);
    }

    public void SetInputEnabled(bool enabled)
    {
        _isInputEnabled = enabled;

        if (!enabled)
        {
            _isSprinting = false;
            _currentSpeed = _moveSpeed;

            if (_animator != null)
            {
                _animator.SetFloat("MoveX", 0f);
                _animator.SetFloat("MoveY", 0f);
                _animator.SetBool("IsSprinting", false);
                _animator.SetBool("IsMoving", false);
                _animator.SetFloat("Speed", 0f);
            }
        }
    }

    public bool IsInputEnabled => _isInputEnabled;

    public void Teleport(Vector3 targetPosition, Quaternion targetRotation, float cameraPitch = 0f)
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        _cameraPitch = cameraPitch;
        _targetRecoil = Vector2.zero;
        _currentRecoil = Vector2.zero;

        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }

        if (_headTransform != null)
        {
            _headTransform.localRotation = Quaternion.Euler(GetClampedHeadPitch(_cameraPitch), 0f, 0f);
        }

        if (_characterController != null)
        {
            _characterController.enabled = true;
        }

        if (IsSpawned && IsOwner)
        {
            _networkCameraPitch.Value = cameraPitch;
        }
    }

    [ClientRpc]
    public void TeleportClientRpc(Vector3 targetPosition, Quaternion targetRotation, float cameraPitch, ClientRpcParams clientRpcParams = default)
    {
        Teleport(targetPosition, targetRotation, cameraPitch);
    }

    #region Knockback APIs

    public void ApplyKnockback(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (!IsSpawned || IsOwner)
        {
            ApplyKnockbackInternal(force);
        }
        else if (IsServer)
        {
            ApplyKnockbackClientRpc(force);
        }
    }

    public void ApplyExplosionKnockback(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardsModifier = 0.5f)
    {
        Vector3 dir = (transform.position - explosionPosition);
        float distance = dir.magnitude;
        if (distance > explosionRadius || distance < 0.001f)
        {
            return;
        }

        dir /= distance;
        dir.y += upwardsModifier;
        dir.Normalize();

        float falloff = 1f - (distance / explosionRadius);
        Vector3 force = dir * (explosionForce * falloff);

        ApplyKnockback(force);
    }

    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector3 force)
    {
        if (IsOwner)
        {
            ApplyKnockbackInternal(force);
        }
    }

    private void ApplyKnockbackInternal(Vector3 force)
    {
        _knockbackVelocity = force;
        _knockbackTimer = 0.35f;
        _isGrounded = false;
    }

    #endregion
}
