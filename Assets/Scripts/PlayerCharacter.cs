using System;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 월드 상에 물리적/시각적으로 존재하는 플레이어 캐릭터(아바타) 컴포넌트입니다.
/// Rigidbody 기반의 물리 이동, 점프, 스태미나, 1인칭/3인칭 머리 회전 및 피치 동기화, 피격 및 반동 처리를 담당합니다.
/// 네트워크 상의 플레이어 세션(NetworkPlayer)에 의해 빙의(Possess)되어 제어됩니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
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

    [Header("Head Settings")]
    [SerializeField] protected Transform _headTransform;
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
    [Tooltip("비워둘 경우 기본 InputActionAsset(InputSystem_Actions)에서 'Player/Jump'를 자동으로 로드합니다.")]
    [SerializeField] private InputActionReference _jumpActionReference;

    [Header("Input Asset Fallback")]
    [SerializeField] private InputActionAsset _inputActionsAsset;

    protected Rigidbody _rigidbody;
    protected CapsuleCollider _capsuleCollider;
    [Obsolete("CharacterController is deprecated. Use Rigidbody for physics movement.")]
    protected CharacterController _characterController;
    protected ClientNetworkTransform _clientNetworkTransform;
    protected Animator _animator;
    protected PlayerCharacterIK _characterIK;
    protected InputAction _moveAction;
    protected InputAction _lookAction;
    protected InputAction _sprintAction;
    protected InputAction _jumpAction;
    protected float _cameraPitch;
    protected bool _isInputEnabled = true;

    // 물리 이동 및 지면 감지 상태
    protected bool _isGrounded;
    protected RaycastHit _groundHit;
    private bool _isJumpQueued;
    private float _jumpCooldownTimer;
    private float _knockbackTimer;
    private PhysicsMaterial _frictionlessMaterial;

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

    // 반동 복구 제어 변수
    private float _recoilPitchRecoveryVelocity;
    private float _recoilYawRecoveryVelocity;
    private float _currentRecoilPitchOffset = 0f;
    private float _currentRecoilYawOffset = 0f;

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
    public float CameraPitch => IsOwner ? _cameraPitch : _networkCameraPitch.Value;
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
        // 1. 레거시 CharacterController는 물리 연산(PhysX) 충돌 방지를 위해 즉시 제거
        _characterController = GetComponent<CharacterController>();
        if (_characterController != null)
        {
            _characterController.enabled = false;
            Destroy(_characterController);
            _characterController = null;
        }

        // 2. 지면 레이어 기본값 검증 (인스펙터 미할당 시 전체 레이어 사용)
        if (_groundLayers.value == 0)
        {
            _groundLayers = ~0;
        }

        // 3. Rigidbody 설정 (Y축 회전은 자유롭게 두어 마우스 회전 시 물리 튐 방지)
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        _rigidbody.mass = _mass;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rigidbody.useGravity = true;

        // 4. CapsuleCollider 설정
        _capsuleCollider = GetComponent<CapsuleCollider>();
        if (_capsuleCollider == null)
        {
            _capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        }
        _capsuleCollider.center = new Vector3(0f, 1f, 0f);
        _capsuleCollider.height = 2f;
        _capsuleCollider.radius = 0.45f;

        // 벽에 비벼도 달라붙지 않도록 마찰력 0 머티리얼 적용
        _frictionlessMaterial = new PhysicsMaterial("PlayerFrictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        _capsuleCollider.material = _frictionlessMaterial;

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
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }

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
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }

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
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }

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
        // 1인칭 카메라는 CameraTarget의 자식이므로 타겟 추적을 해제하고 로컬 (0,0,0)에 고정
        if (_firstPersonCamera != null)
        {
            _firstPersonCamera.Target.TrackingTarget = null;
            _firstPersonCamera.Target.LookAtTarget = null;
            _firstPersonCamera.transform.localPosition = Vector3.zero;
            _firstPersonCamera.transform.localRotation = Quaternion.identity;
            _firstPersonCamera.gameObject.SetActive(true);
            _firstPersonCamera.Priority.Value = 20;
        }

        // 씬 카메라 컨트롤러가 존재하면 캐릭터 머리를 타겟으로 설정
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
                // 손에 든 무기 비주얼(HeldItemVisual_1P, HeldItemVisual_3P)은 PlayerItemHolder가 자체 관리하므로 제외
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

            // 캐릭터가 사라질 때 카메라를 기지 코어 관전 시점으로 전환
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

        if (_frictionlessMaterial != null)
        {
            Destroy(_frictionlessMaterial);
            _frictionlessMaterial = null;
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

        if (_moveAction != null)
        {
            _moveAction.Enable();
        }
        if (_lookAction != null)
        {
            _lookAction.Enable();
        }
        if (_sprintAction != null)
        {
            _sprintAction.Enable();
        }
        if (_jumpAction != null)
        {
            _jumpAction.Enable();
        }
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
        HandleSprintAndStamina();
        HandleJumpInput();
        UpdateCameraFov();
    }

    protected virtual void FixedUpdate()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        UpdateGroundCheck();
        HandlePhysicsMovement();
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

    private void UpdateGroundCheck()
    {
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= Time.fixedDeltaTime;
            _isGrounded = false;
            return;
        }

        // Player 레이어 제외 마스크
        int playerLayerMask = 1 << gameObject.layer;
        LayerMask mask = _groundLayers & ~playerLayerMask;

        // 1. 먼저 정중앙에서 아래로 정밀 Raycast 시도 (타일 솔기 모서리 오감지 및 노멀 튐 방지)
        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
        float rayDistance = 0.2f + _groundCheckDistance + 0.1f;

        RaycastHit[] rayHits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, mask, QueryTriggerInteraction.Ignore);
        _isGrounded = false;
        foreach (var hit in rayHits)
        {
            if (hit.collider == null || hit.collider.transform.root == transform) continue;
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle <= _slopeLimit)
            {
                _groundHit = hit;
                _isGrounded = true;
                return;
            }
        }

        // 2. 중앙 Raycast가 빗나간 경우(계단이나 난간 모서리에 걸친 경우) 보조로 SphereCast 수행
        Vector3 origin = transform.position + Vector3.up * (_groundCheckRadius + 0.1f);
        float castDistance = _groundCheckDistance + 0.1f;

        RaycastHit[] hits = Physics.SphereCastAll(origin, _groundCheckRadius, Vector3.down, castDistance, mask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.root == transform) continue;
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle <= _slopeLimit)
            {
                _groundHit = hit;
                _isGrounded = true;
                break;
            }
        }
    }

    private void HandlePhysicsMovement()
    {
        if (_rigidbody == null || _rigidbody.isKinematic)
        {
            return;
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

        float effectiveSpeed = _currentSpeed * _speedPenaltyMultiplier;

        // 1. 점프 처리
        if (_isJumpQueued && _isGrounded)
        {
            _isJumpQueued = false;
            _jumpCooldownTimer = 0.25f;
            _isGrounded = false;

            Vector3 currentVel = _rigidbody.linearVelocity;
            _rigidbody.linearVelocity = new Vector3(currentVel.x, _jumpForce, currentVel.z);
        }

        // 2. 넉백 진행 중일 때: 입력에 의한 강제 감속을 막고 미세한 공중 제어만 허용
        if (_knockbackTimer > 0f)
        {
            if (move.sqrMagnitude > 0.01f)
            {
                _rigidbody.AddForce(move * (effectiveSpeed * _airControl * 0.5f), ForceMode.Acceleration);
            }
            return;
        }

        // 3. 지면 및 공중 이동 처리
        if (_isGrounded)
        {
            float slopeAngle = Vector3.Angle(_groundHit.normal, Vector3.up);
            Vector3 currentVel = _rigidbody.linearVelocity;

            // [상용 물리 게임 표준 패턴: 축 분리 (Decoupled Axes)]
            // 평지(경사각 1도 미만)에서는 수평 XZ축만 조작하고, Y축 속도는 PhysX의 안정된 접촉 솔버에 위임하여
            // 중력과 충돌 반발력 간의 50Hz 미세 진동(침투-튕김)을 완전히 제거합니다.
            if (slopeAngle < 1.0f)
            {
                Vector3 targetHorizontal = move * effectiveSpeed;
                Vector3 currentHorizontal = new Vector3(currentVel.x, 0f, currentVel.z);

                Vector3 newHorizontal = Vector3.MoveTowards(
                    currentHorizontal,
                    targetHorizontal,
                    _accelerationRate * Time.fixedDeltaTime
                );

                // 평지에서 튀어 오르는 것을 방지하기 위해 미세한 상향 튐만 0으로 완화
                float yVel = currentVel.y;
                if (yVel > 0.05f)
                {
                    yVel = 0f;
                }

                _rigidbody.linearVelocity = new Vector3(newHorizontal.x, yVel, newHorizontal.z);
            }
            else
            {
                // 경사면일 때는 경사면을 따라 미끄러지거나 뜨지 않도록 경사면 투영 속도 적용
                Vector3 slopeMoveDir = Vector3.ProjectOnPlane(move, _groundHit.normal).normalized;
                Vector3 targetVelocity = slopeMoveDir * (effectiveSpeed * move.magnitude);

                Vector3 currentHorizontal = new Vector3(currentVel.x, 0f, currentVel.z);
                Vector3 targetHorizontal = new Vector3(targetVelocity.x, 0f, targetVelocity.z);

                Vector3 newHorizontal = Vector3.MoveTowards(
                    currentHorizontal,
                    targetHorizontal,
                    _accelerationRate * Time.fixedDeltaTime
                );

                float yVel = targetVelocity.y;
                if (move.sqrMagnitude <= 0.01f)
                {
                    yVel = 0f; // 경사면 정지 시 미끄러짐 방지
                }

                _rigidbody.linearVelocity = new Vector3(newHorizontal.x, yVel, newHorizontal.z);
            }
        }
        else
        {
            // 급경사 미끄러짐 처리
            if (_groundHit.collider != null && Vector3.Angle(_groundHit.normal, Vector3.up) > _slopeLimit)
            {
                Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, _groundHit.normal).normalized;
                _rigidbody.AddForce(slideDir * 20f, ForceMode.Acceleration);
            }

            // 공중 제어 (Air Control)
            if (move.sqrMagnitude > 0.01f)
            {
                Vector3 airForce = move * (effectiveSpeed * _airControl);
                _rigidbody.AddForce(airForce, ForceMode.Acceleration);

                // 수평 최대 속도 제한
                Vector3 horiz = new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);
                if (horiz.sqrMagnitude > effectiveSpeed * effectiveSpeed)
                {
                    horiz = horiz.normalized * effectiveSpeed;
                    _rigidbody.linearVelocity = new Vector3(horiz.x, _rigidbody.linearVelocity.y, horiz.z);
                }
            }

            // 낙하 가속도 (빠른 착지감 제공)
            if (_rigidbody.linearVelocity.y < 0f && _fallGravityMultiplier > 1f)
            {
                _rigidbody.AddForce(Physics.gravity * (_fallGravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        // 애니메이터 파라미터 업데이트
        if (_animator != null)
        {
            _animator.SetFloat("MoveX", moveInput.x, 0.1f, Time.fixedDeltaTime);
            _animator.SetFloat("MoveY", moveInput.y, 0.1f, Time.fixedDeltaTime);
            _animator.SetBool("IsSprinting", _isSprinting);
            _animator.SetBool("IsMoving", moveInput.sqrMagnitude > 0.01f);
            _animator.SetFloat("Speed", moveInput.sqrMagnitude > 0.01f ? effectiveSpeed : 0f);
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

        if (IsSpawned && IsOwner)
        {
            _networkCameraPitch.Value = _cameraPitch;
        }

        // Rigidbody를 통한 안전한 수평 물리 회전 (FreezeRotationY 방지 및 PhysX 동기화)
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            Quaternion newRot = transform.rotation * Quaternion.Euler(0f, mouseX, 0f);
            _rigidbody.MoveRotation(newRot);
            transform.rotation = newRot;
        }
        else
        {
            transform.Rotate(Vector3.up * mouseX);
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

    public void ApplyRecoil(float pitchOffset, float yawOffset, float recoverySpeed = 10f)
    {
        if (!IsOwner && IsSpawned)
        {
            return;
        }

        _cameraPitch -= pitchOffset;
        _cameraPitch = Mathf.Clamp(_cameraPitch, _bottomClamp, _topClamp);

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

        if (Mathf.Abs(yawOffset) > 0.001f)
        {
            transform.Rotate(Vector3.up * yawOffset);
        }
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

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = targetPosition;
            _rigidbody.rotation = targetRotation;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        _cameraPitch = cameraPitch;
        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
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

    /// <summary>
    /// 외부 컴포넌트(폭발, 몬스터 피격, 트랩 등)에서 캐릭터에 충격량을 가할 때 호출합니다.
    /// 네트워크 소유자(Owner)의 Rigidbody에 즉시 물리 힘이 전달됩니다.
    /// </summary>
    /// <param name="force">가할 물리 힘 벡터 (방향 * 세기)</param>
    /// <param name="mode">힘 적용 모드 (기본값: ForceMode.Impulse)</param>
    public void ApplyKnockback(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (!IsSpawned || IsOwner)
        {
            ApplyKnockbackInternal(force, mode);
        }
        else if (IsServer)
        {
            ApplyKnockbackClientRpc(force, mode);
        }
    }

    /// <summary>
    /// 폭발 중심점과 반경을 기반으로 폭발 넉백을 가합니다.
    /// </summary>
    public void ApplyExplosionKnockback(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardsModifier = 0.5f)
    {
        if (!IsSpawned || IsOwner)
        {
            ApplyExplosionKnockbackInternal(explosionPosition, explosionForce, explosionRadius, upwardsModifier);
        }
        else if (IsServer)
        {
            ApplyExplosionKnockbackClientRpc(explosionPosition, explosionForce, explosionRadius, upwardsModifier);
        }
    }

    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector3 force, ForceMode mode)
    {
        if (IsOwner)
        {
            ApplyKnockbackInternal(force, mode);
        }
    }

    [ClientRpc]
    private void ApplyExplosionKnockbackClientRpc(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardsModifier)
    {
        if (IsOwner)
        {
            ApplyExplosionKnockbackInternal(explosionPosition, explosionForce, explosionRadius, upwardsModifier);
        }
    }

    private void ApplyKnockbackInternal(Vector3 force, ForceMode mode)
    {
        if (_rigidbody == null || _rigidbody.isKinematic)
        {
            return;
        }

        // 지면 마찰로 인해 넉백이 씹히지 않도록 공중으로 살짝 띄우고 넉백 타이머 활성화
        _knockbackTimer = 0.35f;
        _isGrounded = false;

        // 수평 힘만 강하게 들어올 경우 최소 Y축 상승력을 보장하여 통쾌하게 날아가도록 보정
        if (force.y < 1f && force.sqrMagnitude > 4f)
        {
            force += Vector3.up * Mathf.Clamp(force.magnitude * 0.25f, 1.5f, 5f);
        }

        _rigidbody.AddForce(force, mode);
    }

    private void ApplyExplosionKnockbackInternal(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardsModifier)
    {
        if (_rigidbody == null || _rigidbody.isKinematic)
        {
            return;
        }

        _knockbackTimer = 0.4f;
        _isGrounded = false;
        _rigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardsModifier, ForceMode.Impulse);
    }

    #endregion
}
