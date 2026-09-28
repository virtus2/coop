using System;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 월드 상에 물리적/시각적으로 존재하는 플레이어 캐릭터(아바타) 컴포넌트입니다.
/// 캐릭터의 이동, 점프, 스태미나, 1인칭/3인칭 머리 회전 및 피치 동기화, 피격 및 반동 처리를 담당합니다.
/// 네트워크 상의 플레이어 세션(NetworkPlayer)에 의해 빙의(Possess)되어 제어됩니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerCharacter : NetworkBehaviour
{
    public static PlayerCharacter LocalInstance { get; protected set; }

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

    [Header("Input Asset Fallback")]
    [SerializeField] private InputActionAsset _inputActionsAsset;

    protected CharacterController _characterController;
    protected ClientNetworkTransform _clientNetworkTransform;
    protected Animator _animator;
    protected PlayerCharacterIK _characterIK;
    protected InputAction _moveAction;
    protected InputAction _lookAction;
    protected InputAction _sprintAction;
    protected Vector3 _velocity;
    protected float _cameraPitch;
    protected bool _isInputEnabled = true;

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
        _characterController = GetComponent<CharacterController>();
        _clientNetworkTransform = GetComponent<ClientNetworkTransform>();

        if (GetComponent<PlayerNamePlate>() == null)
        {
            gameObject.AddComponent<PlayerNamePlate>();
        }
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
        }
        _characterController.center = new Vector3(0f, 1f, 0f);
        _characterController.height = 2f;
        _characterController.radius = 0.5f;

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
        // 씬 카메라 컨트롤러가 존재하면 캐릭터 머리를 타겟으로 설정
        if (PlayerCameraController.Instance != null && _cameraTarget != null)
        {
            PlayerCameraController.Instance.SetCharacterTarget(_cameraTarget);
        }
        else if (_firstPersonCamera != null)
        {
            _firstPersonCamera.gameObject.SetActive(true);
            _firstPersonCamera.Priority.Value = 20;
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
        HandleMovement();
        UpdateCameraFov();
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

        transform.Rotate(Vector3.up * mouseX);
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

    private void HandleMovement()
    {
        if (_characterController == null)
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
        _characterController.Move(move * effectiveSpeed * Time.deltaTime);

        if (_characterController.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f;
        }

        _velocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);

        if (_animator != null)
        {
            _animator.SetFloat("MoveX", moveInput.x);
            _animator.SetFloat("MoveY", moveInput.y);
            _animator.SetBool("IsSprinting", _isSprinting);
            _animator.SetBool("IsMoving", moveInput.sqrMagnitude > 0.01f);
            _animator.SetFloat("Speed", moveInput.sqrMagnitude > 0.01f ? effectiveSpeed : 0f);
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
}
