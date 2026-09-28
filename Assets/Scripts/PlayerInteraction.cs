using System.Collections.Generic;
using Coop.Rendering;
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

    [Header("Dismantle Settings")]
    [SerializeField] private float _dismantleDuration = 1.2f;
    [SerializeField] private float _dismantleMaxDistance = 3.5f;
    private float _dismantleTimer = 0f;
    private PlaceableObject _currentDismantleTarget;

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
    private PlayerItemHolder _itemHolder;
    public PlayerItemHolder ItemHolder
    {
        get
        {
            if (_itemHolder == null) _itemHolder = GetComponent<PlayerItemHolder>();
            return _itemHolder;
        }
    }

    public bool IsHoldingItem => (ItemHolder != null && ItemHolder.IsHoldingItem) || _heldItem != null;
    public PickableItem HeldItem => _heldItem;
    public IFireable FireableItem => _fireableItem;
    public IUsable UsableItem => _usableItem;
    public bool IsUsingItem => _isUsingItem;

    private void Awake()
    {
        InitializeHoldPoint();
        if (_itemHolder == null) _itemHolder = GetComponent<PlayerItemHolder>();
        if (GetComponent<PlayerGunCombat>() == null)
        {
            gameObject.AddComponent<PlayerGunCombat>();
        }
        if (GetComponent<PlayerMeleeCombat>() == null)
        {
            gameObject.AddComponent<PlayerMeleeCombat>();
        }
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
            ClearCurrentTarget();
            if (InteractionUI.Instance != null)
            {
                InteractionUI.Instance.HideHeldHint();
            }
        }
    }

    private void OnDisable()
    {
        ClearCurrentTarget();
    }

    public override void OnDestroy()
    {
        if (IsOwner || !IsSpawned)
        {
            UnsubscribeInput();
            ClearCurrentTarget();
            if (InteractionUI.Instance != null)
            {
                InteractionUI.Instance.HideHeldHint();
            }
        }
        base.OnDestroy();
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

        // 설치된 블록 F키 홀드 철거 처리
        HandleDismantleUpdate();

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

    private bool IsUIBlocking()
    {
        if (InventoryUIController.Instance != null && InventoryUIController.Instance.IsOpen)
        {
            return true;
        }

        if (InGameMenuController.Instance != null && InGameMenuController.Instance.IsMenuOpen)
        {
            return true;
        }

        return false;
    }

    private void UpdateAimRaycast()
    {
        // UI가 열려있는 경우 월드 오브젝트 상호작용 및 외곽선 강조 즉시 중단
        if (IsUIBlocking())
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

            // 상호작용 가능 여부와 무관하게 사거리 내 인터랙터블이면 탐색 대상에 포함 (불가 시 빨간 외곽선 표시)
            if (interactable != null)
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

        bool canInteract = _currentTarget.CanInteract(this);
        Color outlineColor = canInteract ? Color.white : new Color(1.0f, 0.2f, 0.2f, 1.0f);
        Renderer[] renderers = _currentTarget.GetHighlightRenderers();

        OutlineManager.SetHighlight(renderers, outlineColor);

        if (InteractionUI.Instance != null)
        {
            string prompt = _currentTarget.GetInteractionPrompt();
            Color textColor = canInteract ? Color.white : new Color(1.0f, 0.35f, 0.35f, 1.0f);
            InteractionUI.Instance.ShowPrompt("E", string.IsNullOrEmpty(prompt) ? "상호작용" : prompt, textColor);
        }
    }

    private void ClearCurrentTarget()
    {
        if (_currentTarget != null || OutlineManager.HasTarget)
        {
            _currentTarget = null;
            OutlineManager.ClearHighlight();
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
        if (!IsHoldingItem)
        {
            return;
        }

        // 1. 내려놓기 단축키 (Q키)
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            DropHeldItem();
            return;
        }

        // 2. 발사 또는 사용 기능이 없는 일반 아이템: 마우스 좌클릭으로 버리지 않으며 오직 Q키로만 투척 가능
        if (_fireableItem == null && _usableItem == null)
        {
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

    private class GunAction : IFireable
    {
        public bool CanFire(PlayerInteraction player) => true;
        public void Fire(PlayerInteraction player)
        {
            Debug.Log($"<color=orange>[SampleGunItem]</color> '샘플 권총' 발사! 빵! (플레이어: {player.name})");
        }
    }

    private class MedkitAction : IUsable
    {
        public float RequiredHoldDuration => 1.5f;
        public bool CanUse(PlayerInteraction player) => true;
        public void OnUseStart(PlayerInteraction player)
        {
            Debug.Log($"<color=green>[SampleMedkitItem]</color> 구급키트 사용 시작... (홀드 1.5초 필요)");
        }
        public void OnUseUpdate(PlayerInteraction player, float currentHoldDuration) { }
        public void OnUseEnd(PlayerInteraction player, bool completed)
        {
            if (completed)
            {
                Debug.Log($"<color=green>[SampleMedkitItem]</color> 구급키트 사용 완료! 체력이 회복되었습니다.");
            }
            else
            {
                Debug.Log($"<color=yellow>[SampleMedkitItem]</color> 구급키트 사용 취소됨.");
            }
        }
    }

    private class ChargedWeaponAction : IFireable, IUsable
    {
        public float RequiredHoldDuration => 2.0f;
        public bool CanFire(PlayerInteraction player) => true;
        public void Fire(PlayerInteraction player)
        {
            Debug.Log($"<color=cyan>[SampleChargedWeaponItem]</color> 기본 빔 찌릿! (플레이어: {player.name})");
        }
        public bool CanUse(PlayerInteraction player) => true;
        public void OnUseStart(PlayerInteraction player)
        {
            Debug.Log($"<color=cyan>[SampleChargedWeaponItem]</color> 메가 레이저 충전 시작! 위이이잉~ (홀드 2.0초)");
        }
        public void OnUseUpdate(PlayerInteraction player, float currentHoldDuration) { }
        public void OnUseEnd(PlayerInteraction player, bool completed)
        {
            if (completed)
            {
                Debug.Log($"<color=cyan>[SampleChargedWeaponItem]</color> 콰아아앙! 메가 레이저 발사 성공!");
            }
            else
            {
                Debug.Log($"<color=yellow>[SampleChargedWeaponItem]</color> 레이저 충전 취소됨.");
            }
        }
    }

    private readonly GunAction _gunAction = new GunAction();
    private readonly MedkitAction _medkitAction = new MedkitAction();
    private readonly ChargedWeaponAction _chargedWeaponAction = new ChargedWeaponAction();

    public void UpdateHeldHintUI(GameObject heldInstance = null, ItemData itemData = null)
    {
        if (InteractionUI.Instance == null)
        {
            return;
        }

        ItemData currentData = itemData != null ? itemData : (ItemHolder != null ? ItemHolder.CurrentHeldItemData : null);
        if (currentData == null)
        {
            InteractionUI.Instance.HideHeldHint();
            return;
        }

        if (ItemHolder != null && ItemHolder.IsHoldingWorldItem)
        {
            InteractionUI.Instance.ShowHeldHint("[1~0] 툴바 슬롯에 넣기 | [Q] 내려놓기");
            return;
        }

        if (currentData.ActionType == ItemActionType.Placeable || (heldInstance != null && heldInstance.GetComponent<PlaceableItem>() != null))
        {
            string name = currentData.PlaceableBuildingPrefab != null ? currentData.PlaceableBuildingPrefab.DisplayName : currentData.ItemName;
            InteractionUI.Instance.ShowHeldHint($"[우클릭] {name} 설치 | [R] 회전 | [Q] 내려놓기");
            return;
        }

        if (currentData.ActionType == ItemActionType.MeleeWeapon || currentData is MeleeItemData)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭] 휘두르기 (홀드 시 연속 공격) | [Q] 내려놓기");
            return;
        }

        if (currentData.ActionType == ItemActionType.Gun || currentData is GunItemData)
        {
            if (currentData is GunItemData gun && gun.FireMode == GunFireMode.Charge)
            {
                InteractionUI.Instance.ShowHeldHint("[좌클릭 홀드] 차징 발사 | [차징 중 우클릭] 취소 | [R] 재장전 | [Q] 내려놓기");
            }
            else
            {
                InteractionUI.Instance.ShowHeldHint("[좌클릭] 사격 | [R] 재장전 | [Q] 내려놓기");
            }
            return;
        }

        if (_fireableItem != null && _usableItem != null)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭] 발사 | [좌클릭 홀드] 사용 | [Q] 내려놓기");
        }
        else if (_fireableItem != null)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭] 발사 | [Q] 내려놓기");
        }
        else if (_usableItem != null)
        {
            InteractionUI.Instance.ShowHeldHint("[좌클릭 홀드] 사용 | [Q] 내려놓기");
        }
        else
        {
            InteractionUI.Instance.ShowHeldHint("[Q] 내려놓기");
        }
    }

    /// <summary>
    /// PlayerItemHolder에서 손에 든 모델이 변경(장착/해제)되었을 때 호출됩니다.
    /// ItemData.ActionType에 따라 액션 핸들러를 바인딩하고 UI 힌트를 갱신합니다.
    /// </summary>
    public void OnHeldItemChanged(GameObject heldInstance, ItemData itemData)
    {
        ResetItemActionState();

        if (itemData != null)
        {
            switch (itemData.ActionType)
            {
                case ItemActionType.Gun:
                    // 총기 액션은 PlayerGunCombat이 전담하여 발사/장전/반동/차징을 처리합니다.
                    _fireableItem = null;
                    _usableItem = null;
                    break;
                case ItemActionType.MeleeWeapon:
                    // 근접 무기 액션은 PlayerMeleeCombat이 전담하여 공격/넉백/히트를 처리합니다.
                    _fireableItem = null;
                    _usableItem = null;
                    break;
                case ItemActionType.Medkit:
                    _fireableItem = null;
                    _usableItem = _medkitAction;
                    break;
                case ItemActionType.ChargedWeapon:
                    _fireableItem = _chargedWeaponAction;
                    _usableItem = _chargedWeaponAction;
                    break;
                case ItemActionType.Placeable:
                    _fireableItem = null;
                    _usableItem = null;
                    if (GridBuildingController.Instance != null && itemData.PlaceableBuildingPrefab != null)
                    {
                        GridBuildingController.Instance.StartBuilding(itemData, ItemHolder);
                    }
                    break;
                default:
                    _fireableItem = heldInstance != null ? heldInstance.GetComponent<IFireable>() : null;
                    _usableItem = heldInstance != null ? heldInstance.GetComponent<IUsable>() : null;
                    break;
            }

            if (itemData.ActionType != ItemActionType.Placeable)
            {
                var placeableItem = heldInstance != null ? heldInstance.GetComponent<PlaceableItem>() : null;
                if (placeableItem != null && GridBuildingController.Instance != null)
                {
                    GridBuildingController.Instance.StartBuilding(placeableItem);
                }
            }
        }
        else
        {
            _fireableItem = null;
            _usableItem = null;

            if (GridBuildingController.Instance != null)
            {
                GridBuildingController.Instance.StopBuilding();
            }
        }

        UpdateHeldHintUI(heldInstance, itemData);
    }

    /// <summary>
    /// 월드에 놓인 물리 아이템을 획득합니다. (E키 상호작용)
    /// </summary>
    public void PickupWorldItem(PickableItem worldItem)
    {
        if (worldItem == null)
        {
            return;
        }

        if (ItemHolder != null)
        {
            bool pickedUp = ItemHolder.PickupWorldItem(worldItem);
            if (pickedUp)
            {
                ClearCurrentTarget();
            }
        }
    }

    /// <summary>
    /// 하위 호환성 지원용 (레거시 호출 대비)
    /// </summary>
    public void OnItemPickedUp(PickableItem item)
    {
        PickupWorldItem(item);
    }

    /// <summary>
    /// 하위 호환성 지원용 (레거시 호출 대비)
    /// </summary>
    public void OnItemDropped(PickableItem item)
    {
        OnHeldItemChanged(null, null);
    }

    /// <summary>
    /// 손에 들고 있는 물체를 플레이어가 바라보는 시선 방향으로 물리적으로 던집니다.
    /// 멀티플레이어 환경에서 서버 RPC를 통해 WorldPrefab 스폰 및 동일한 물리 속도를 부여합니다.
    /// </summary>
    public void DropHeldItem()
    {
        // 근접 공격 모션 진행 중에는 드롭 불가 (E-05)
        if (PlayerMeleeCombat.LocalInstance != null && PlayerMeleeCombat.LocalInstance.IsAttacking)
        {
            return;
        }

        if (PlayerGunCombat.LocalInstance != null)
        {
            PlayerGunCombat.LocalInstance.CancelCharge();
            PlayerGunCombat.LocalInstance.CancelReload();
        }

        if (!IsHoldingItem)
        {
            return;
        }

        // 1. 카메라 시선 방향 및 투척 시작 위치 계산
        Vector3 lookDir = _mainCamera != null ? _mainCamera.transform.forward : transform.forward;
        Vector3 eyePos = _mainCamera != null ? _mainCamera.transform.position : transform.position + Vector3.up * 1.5f;

        // 벽 뚫림 방지: 전방 0.6m 내에 벽/장애물이 있으면 거리를 좁혀 안전한 위치에서 투척 시작
        float spawnDistance = 0.5f;
        if (Physics.Raycast(eyePos, lookDir, out RaycastHit wallHit, 0.6f, _interactionLayerMask, QueryTriggerInteraction.Ignore))
        {
            spawnDistance = Mathf.Max(0.1f, wallHit.distance - 0.15f);
        }

        Vector3 throwOrigin = eyePos + lookDir * spawnDistance;
        Quaternion throwRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        // 2. 투척 속도 벡터 계산 (시선 방향 6.0m/s + 상향 1.5m/s 포물선 호)
        Vector3 throwVelocity = lookDir * 6.0f + Vector3.up * 1.5f;

        ResetItemActionState();

        if (InteractionUI.Instance != null)
        {
            InteractionUI.Instance.HideHeldHint();
        }

        if (ItemHolder != null)
        {
            ItemHolder.DropCurrentHeldItem(throwOrigin, throwRotation, throwVelocity);
        }
    }

    /// <summary>
    /// 손 상태와 무관하게 설치된 PlaceableObject를 바라보고 F키를 꾹(1.2초) 누르면 게이지가 차오르고 철거를 수행합니다.
    /// </summary>
    private void HandleDismantleUpdate()
    {
        if (_mainCamera == null)
        {
            return;
        }

        Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        PlaceableObject hitPlaceable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, _dismantleMaxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            hitPlaceable = hit.collider.GetComponentInParent<PlaceableObject>();
            if (hitPlaceable == null)
            {
                hitPlaceable = hit.collider.GetComponent<PlaceableObject>();
            }
        }

        bool isFKeyPressed = Keyboard.current != null && Keyboard.current.fKey.isPressed;

        if (hitPlaceable != null && isFKeyPressed)
        {
            if (_currentDismantleTarget != hitPlaceable)
            {
                _currentDismantleTarget = hitPlaceable;
                _dismantleTimer = 0f;
            }

            _dismantleTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_dismantleTimer / _dismantleDuration);

            if (DismantleProgressUI.Instance != null)
            {
                DismantleProgressUI.Instance.SetProgress(progress, hitPlaceable.DisplayName);
            }

            if (_dismantleTimer >= _dismantleDuration)
            {
                var target = _currentDismantleTarget;
                _currentDismantleTarget = null;
                _dismantleTimer = 0f;

                if (DismantleProgressUI.Instance != null)
                {
                    DismantleProgressUI.Instance.Hide();
                }

                target.Dismantle();
            }
        }
        else
        {
            if (_dismantleTimer > 0f || _currentDismantleTarget != null)
            {
                _dismantleTimer = 0f;
                _currentDismantleTarget = null;

                if (DismantleProgressUI.Instance != null)
                {
                    DismantleProgressUI.Instance.Hide();
                }
            }
        }
    }
}
