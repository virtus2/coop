using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로컬 플레이어의 조준선(1인칭 화면 중앙)을 기반으로 상호작용 가능한 물체를 탐색하고,
/// E키 입력 시 상호작용 실행 및 물체 들기/내려놓기를 담당하는 컴포넌트입니다.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Interaction Raycast Settings")]
    [SerializeField] private float _interactionRange = 4.5f;
    [SerializeField] private LayerMask _interactionLayerMask = ~0; // 기본 전체 레이어
    [SerializeField] private float _maxDropReach = 5.0f;

    [Header("Hold Socket")]
    [SerializeField] private Transform _holdPoint;
    [SerializeField] private Vector3 _defaultHoldPointOffset = new Vector3(0.32f, -0.25f, 0.65f);

    [Header("Input Action References")]
    [SerializeField] private InputActionReference _interactActionReference;
    [SerializeField] private InputActionReference _attackActionReference;
    [SerializeField] private InputActionAsset _inputActionsAsset;

    [Header("Item Action Settings")]
    [Tooltip("복합 아이템(발사 및 홀드 사용 모두 가능)에서 단발 탭과 홀드 사용을 구분하는 기준 시간(초)")]
    [SerializeField] private float _holdThreshold = 0.25f;

    private Camera _mainCamera;
    private IInteractable _currentTarget;
    private PickableItem _heldItem;
    private IFireable _fireableItem;
    private IUsable _usableItem;
    private InputAction _interactAction;
    private InputAction _attackAction;
    private readonly RaycastHit[] _raycastHits = new RaycastHit[10];

    // 홀드 및 발사 액션 추적용 상태 변수
    private bool _isActionPressed;
    private float _actionHoldTimer;
    private bool _isUsingItem;

    public Transform HoldPoint
    {
        get
        {
            if (_holdPoint == null)
            {
                InitializeHoldPoint();
            }
            return _holdPoint;
        }
    }
    public bool IsHoldingItem => _heldItem != null;
    public PickableItem HeldItem => _heldItem;
    public IFireable FireableItem => _fireableItem;
    public IUsable UsableItem => _usableItem;
    public bool IsUsingItem => _isUsingItem;

    private void Awake()
    {
        InitializeHoldPoint();
    }

    private void Start()
    {
        if (IsOwner || !IsSpawned)
        {
            _mainCamera = Camera.main;
            InteractionUI.EnsureInstance();
            InitializeInput();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            _mainCamera = Camera.main;
            InteractionUI.EnsureInstance();
            InitializeInput();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            UnsubscribeInput();
            if (InteractionUI.Instance != null)
            {
                InteractionUI.Instance.HidePrompt();
                InteractionUI.Instance.HideHeldHint();
            }
        }
    }

    private void OnDestroy()
    {
        if (IsOwner || !IsSpawned)
        {
            UnsubscribeInput();
            if (InteractionUI.Instance != null)
            {
                InteractionUI.Instance.HidePrompt();
                InteractionUI.Instance.HideHeldHint();
            }
        }
    }

    private void InitializeHoldPoint()
    {
        if (_holdPoint != null)
        {
            return;
        }

        // CameraTarget 하위에서 HoldPoint 탐색
        Transform cameraTarget = transform.Find("CameraTarget");
        if (cameraTarget != null)
        {
            Transform foundHold = cameraTarget.Find("HoldPoint");
            if (foundHold != null)
            {
                _holdPoint = foundHold;
                return;
            }

            // 없으면 자동 생성하여 시선 방향을 따라가도록 설정
            var holdGO = new GameObject("HoldPoint");
            holdGO.transform.SetParent(cameraTarget, false);
            holdGO.transform.localPosition = _defaultHoldPointOffset;
            holdGO.transform.localRotation = Quaternion.identity;
            _holdPoint = holdGO.transform;
        }
        else
        {
            // CameraTarget이 없는 경우 본체 하위에 생성
            Transform foundHold = transform.Find("HoldPoint");
            if (foundHold != null)
            {
                _holdPoint = foundHold;
                return;
            }

            var holdGO = new GameObject("HoldPoint");
            holdGO.transform.SetParent(transform, false);
            holdGO.transform.localPosition = new Vector3(0.32f, 1.2f, 0.65f);
            _holdPoint = holdGO.transform;
        }
    }

    private void InitializeInput()
    {
        if (_inputActionsAsset == null)
        {
            _inputActionsAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        }

        // Interact Action
        if (_interactActionReference != null && _interactActionReference.action != null)
        {
            _interactAction = _interactActionReference.action;
        }
        else if (_inputActionsAsset != null)
        {
            _interactAction = _inputActionsAsset.FindAction("Player/Interact");
        }

        if (_interactAction != null)
        {
            _interactAction.performed += HandleInteractPerformed;
            _interactAction.Enable();
        }

        // Attack Action (좌클릭)
        if (_attackActionReference != null && _attackActionReference.action != null)
        {
            _attackAction = _attackActionReference.action;
        }
        else if (_inputActionsAsset != null)
        {
            _attackAction = _inputActionsAsset.FindAction("Player/Attack");
        }

        if (_attackAction != null)
        {
            _attackAction.Enable();
        }
    }

    private void UnsubscribeInput()
    {
        if (_interactAction != null)
        {
            _interactAction.performed -= HandleInteractPerformed;
            _interactAction.Disable();
        }

        if (_attackAction != null)
        {
            _attackAction.Disable();
        }
    }

    private void HandleInteractPerformed(InputAction.CallbackContext context)
    {
        if (_currentTarget != null && _currentTarget.CanInteract(this))
        {
            _currentTarget.Interact(this);
        }
    }

    private void Update()
    {
        // 네트워크가 스폰된 경우 로컬 소유자(IsOwner)만 조준 및 입력 처리
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        // 카메라 캐싱 갱신
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return;
            }
        }

        // Fallback 직접 키 입력 확인 (InputAction 미바인딩 상황 대비)
        CheckDirectKeyFallback();

        // 들고 있는 아이템의 발사 및 홀드 사용 입력 처리
        HandleItemActionUpdate();

        // 조준선 레이캐스트 업데이트
        UpdateAimRaycast();
    }

    private void CheckDirectKeyFallback()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (_currentTarget != null && _currentTarget.CanInteract(this))
            {
                _currentTarget.Interact(this);
            }
        }
    }

    private void UpdateAimRaycast()
    {
        // 이미 물체를 들고 있는 경우 상호작용 대상 감지 비활성화
        if (_heldItem != null)
        {
            ClearCurrentTarget();
            return;
        }

        Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        int hitCount = Physics.RaycastNonAlloc(ray, _raycastHits, _interactionRange, _interactionLayerMask, QueryTriggerInteraction.Ignore);

        IInteractable closestInteractable = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _raycastHits[i];

            // 본인 캐릭터 충돌체 무시
            if (hit.collider.transform.root == transform.root)
            {
                continue;
            }

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                interactable = hit.collider.GetComponent<IInteractable>();
            }

            if (interactable != null && interactable.CanInteract(this))
            {
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestInteractable = interactable;
                }
            }
        }

        if (closestInteractable != null)
        {
            SetCurrentTarget(closestInteractable);
        }
        else
        {
            ClearCurrentTarget();
        }
    }

    private void SetCurrentTarget(IInteractable interactable)
    {
        _currentTarget = interactable;
        if (InteractionUI.Instance != null)
        {
            string prompt = _currentTarget.GetInteractionPrompt();
            InteractionUI.Instance.ShowPrompt("E", string.IsNullOrEmpty(prompt) ? "상호작용" : prompt);
        }
    }

    private void ClearCurrentTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget = null;
            if (InteractionUI.Instance != null)
            {
                InteractionUI.Instance.HidePrompt();
            }
        }
    }

    /// <summary>
    /// 플레이어가 들고 있는 아이템의 발사(단발 클릭) 및 홀드 사용(길게 누름) 입력을 처리합니다.
    /// </summary>
    private void HandleItemActionUpdate()
    {
        if (_heldItem == null)
        {
            return;
        }

        // 1. 내려놓기 단축키 (G키)
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropHeldItem();
            return;
        }

        // 2. 발사 또는 사용 기능이 없는 일반 아이템(예: 기본 상자 등): 기존처럼 마우스 좌클릭 시 내려놓기 동작
        if (_fireableItem == null && _usableItem == null)
        {
            if (WasAttackPressedThisFrame())
            {
                DropHeldItem();
            }
            return;
        }

        bool isPressed = IsAttackPressed();
        bool wasPressed = WasAttackPressedThisFrame();
        bool wasReleased = WasAttackReleasedThisFrame();

        // 3. 발사 전용 아이템 (IFireable만 구현)
        if (_fireableItem != null && _usableItem == null)
        {
            if (wasPressed && _fireableItem.CanFire(this))
            {
                _fireableItem.Fire(this);
            }
            return;
        }

        // 4. 사용 전용 아이템 (IUsable만 구현)
        if (_fireableItem == null && _usableItem != null)
        {
            if (wasPressed && _usableItem.CanUse(this))
            {
                _isUsingItem = true;
                _actionHoldTimer = 0f;
                _usableItem.OnUseStart(this);
            }

            if (_isUsingItem)
            {
                if (isPressed)
                {
                    _actionHoldTimer += Time.deltaTime;
                    _usableItem.OnUseUpdate(this, _actionHoldTimer);

                    if (_usableItem.RequiredHoldDuration > 0f && _actionHoldTimer >= _usableItem.RequiredHoldDuration)
                    {
                        _isUsingItem = false;
                        _usableItem.OnUseEnd(this, true);
                    }
                }
                else if (wasReleased)
                {
                    bool completed = _usableItem.RequiredHoldDuration > 0f && _actionHoldTimer >= _usableItem.RequiredHoldDuration;
                    _isUsingItem = false;
                    _usableItem.OnUseEnd(this, completed);
                }
            }
            return;
        }

        // 5. 복합 아이템 (IFireable과 IUsable 둘 다 구현)
        if (_fireableItem != null && _usableItem != null)
        {
            if (wasPressed)
            {
                _isActionPressed = true;
                _actionHoldTimer = 0f;
                _isUsingItem = false;
            }

            if (_isActionPressed)
            {
                if (isPressed)
                {
                    _actionHoldTimer += Time.deltaTime;

                    // 홀드 기준 시간 도달 시 사용 모드 진입
                    if (!_isUsingItem && _actionHoldTimer >= _holdThreshold)
                    {
                        if (_usableItem.CanUse(this))
                        {
                            _isUsingItem = true;
                            _usableItem.OnUseStart(this);
                        }
                    }

                    if (_isUsingItem)
                    {
                        float usableHoldDuration = _actionHoldTimer - _holdThreshold;
                        _usableItem.OnUseUpdate(this, usableHoldDuration);

                        if (_usableItem.RequiredHoldDuration > 0f && usableHoldDuration >= _usableItem.RequiredHoldDuration)
                        {
                            _isUsingItem = false;
                            _isActionPressed = false;
                            _usableItem.OnUseEnd(this, true);
                        }
                    }
                }
                else if (wasReleased)
                {
                    if (_isUsingItem)
                    {
                        float usableHoldDuration = _actionHoldTimer - _holdThreshold;
                        bool completed = _usableItem.RequiredHoldDuration > 0f && usableHoldDuration >= _usableItem.RequiredHoldDuration;
                        _isUsingItem = false;
                        _usableItem.OnUseEnd(this, completed);
                    }
                    else
                    {
                        // 홀드 기준 시간 이전에 뗐으므로 단발 클릭 발사 실행
                        if (_fireableItem.CanFire(this))
                        {
                            _fireableItem.Fire(this);
                        }
                    }

                    _isActionPressed = false;
                    _actionHoldTimer = 0f;
                }
            }
        }
    }

    private bool IsAttackPressed()
    {
        if (_attackAction != null && _attackAction.IsPressed())
        {
            return true;
        }
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private bool WasAttackPressedThisFrame()
    {
        if (_attackAction != null && _attackAction.WasPressedThisFrame())
        {
            return true;
        }
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool WasAttackReleasedThisFrame()
    {
        if (_attackAction != null && _attackAction.WasReleasedThisFrame())
        {
            return true;
        }
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    private void ResetItemActionState()
    {
        if (_isUsingItem && _usableItem != null)
        {
            _usableItem.OnUseEnd(this, false);
        }
        _isActionPressed = false;
        _actionHoldTimer = 0f;
        _isUsingItem = false;
    }

    private void UpdateHeldHintUI()
    {
        if (InteractionUI.Instance == null || _heldItem == null)
        {
            return;
        }

        if (_fireableItem != null && _usableItem != null)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭] 발사 | [좌클릭 홀드] 사용 | [G] 내려놓기");
        }
        else if (_fireableItem != null)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭] 발사 | [G] 내려놓기");
        }
        else if (_usableItem != null)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭 홀드] 사용 | [G] 내려놓기");
        }
        else
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭 / G] 내려놓기");
        }
    }

    /// <summary>
    /// PickableItem에서 플레이어가 아이템을 주웠을 때 호출됩니다.
    /// </summary>
    public void OnItemPickedUp(PickableItem item)
    {
        _heldItem = item;
        _fireableItem = item != null ? item.GetComponent<IFireable>() : null;
        _usableItem = item != null ? item.GetComponent<IUsable>() : null;
        ClearCurrentTarget();
        ResetItemActionState();
        UpdateHeldHintUI();
    }

    /// <summary>
    /// PickableItem에서 아이템이 내려놓아졌을 때 호출됩니다.
    /// </summary>
    public void OnItemDropped(PickableItem item)
    {
        if (_heldItem == null || _heldItem == item)
        {
            ResetItemActionState();
            _heldItem = null;
            _fireableItem = null;
            _usableItem = null;
        }

        if (InteractionUI.Instance != null)
        {
            InteractionUI.Instance.HideHeldHint();
        }
    }

    /// <summary>
    /// 손에 들고 있는 물체를 조준선 방향의 지면에 내려놓습니다.
    /// </summary>
    public void DropHeldItem()
    {
        if (_heldItem == null)
        {
            return;
        }

        Vector3 dropPosition = Vector3.zero;
        Quaternion dropRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Ray ray = _mainCamera != null
            ? _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);

        // 1. 시선 방향 레이캐스트로 닿는 지면 확인
        bool hasHit = false;
        if (Physics.Raycast(ray, out RaycastHit hit, _maxDropReach, _interactionLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root != transform.root)
            {
                dropPosition = hit.point + hit.normal * _heldItem.DropVerticalOffset;
                hasHit = true;
            }
        }

        if (!hasHit)
        {
            // 2. 허공이나 먼 곳을 보고 있을 경우: 플레이어 앞쪽 1.5m 위치에서 아래로 레이캐스트
            Vector3 checkPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            if (Physics.Raycast(checkPos, Vector3.down, out RaycastHit downHit, 2.5f, _interactionLayerMask, QueryTriggerInteraction.Ignore))
            {
                dropPosition = downHit.point + Vector3.up * _heldItem.DropVerticalOffset;
            }
            else
            {
                // 바닥 감지 실패 시 발밑 앞쪽에 배치
                dropPosition = transform.position + transform.forward * 1.0f + Vector3.up * _heldItem.DropVerticalOffset;
            }
        }

        PickableItem itemToDrop = _heldItem;
        ResetItemActionState();
        _heldItem = null;
        _fireableItem = null;
        _usableItem = null;

        if (InteractionUI.Instance != null)
        {
            InteractionUI.Instance.HideHeldHint();
        }

        itemToDrop.Drop(dropPosition, dropRotation);
    }
}
