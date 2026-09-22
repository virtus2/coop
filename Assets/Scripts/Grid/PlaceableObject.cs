using System;
using System.Collections.Generic;
using Coop.Audio;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 그리드 시스템 상에 배치할 수 있는 건축물/블록 오브젝트입니다.
/// NetworkBehaviour 및 IDamageable을 구현하여 네트워크 동기화, 피격 파괴,
/// NavMesh 길막기(NavMeshObstacle), 올라타기 방지 및 F키 철거 회수를 지원합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlaceableObject : NetworkBehaviour, IDamageable
{
    [Header("Grid Footprint Settings")]
    [Tooltip("오브젝트가 그리드 상에서 차지하는 기본 크기 (X: 가로 셀 수, Y: 세로/깊이 셀 수)")]
    [SerializeField] private Vector2Int _gridSize = new Vector2Int(1, 1);

    [Tooltip("오브젝트 식별용 표시 이름")]
    [SerializeField] private string _displayName = "Buildable Object";

    [Header("Health & Combat Settings")]
    [Tooltip("블록의 최대 내구도(HP)")]
    [SerializeField] private int _maxHealth = 150;

    [Tooltip("플레이어의 일반 사격/공격에 데미지를 입을지 여부 (기본: false - 몬스터 공격에만 피격)")]
    [SerializeField] private bool _allowPlayerDamage = false;

    [Tooltip("폭발물 공격에 데미지를 입을지 여부")]
    [SerializeField] private bool _allowExplosiveDamage = true;

    [Tooltip("파괴 시 생성될 파편/먼지 이펙트 프리팹")]
    [SerializeField] private GameObject _destructionEffectPrefab;

    [Tooltip("파괴 시 재생될 사운드 효과 (SoundCue)")]
    [SerializeField] private SoundCue _destructionSoundCue;

    [Tooltip("철거/회수 시 바닥에 드롭될 아이템 프리팹 (PickableItem)")]
    [SerializeField] private GameObject _droppedItemPrefab;

    [Header("NavMesh & Climb Prevention")]
    [Tooltip("설치 시 적 몬스터의 길찾기를 차단할 NavMeshObstacle 자동 구성 여부")]
    [SerializeField] private bool _setupNavMeshObstacle = true;

    [Tooltip("플레이어가 블록 위로 점프해 올라타는 것을 방지할 차단 콜라이더 높이 (0이면 비활성화)")]
    [SerializeField] private float _antiClimbHeight = 2.5f;

    [Header("Preview / Visuals")]
    [Tooltip("선택 사항: 프리뷰 고스트 생성 시 사용할 메인 렌더러 (비워둘 경우 자식 렌더러 자동 탐색)")]
    [SerializeField] private Renderer _previewRenderer;

    private readonly NetworkVariable<int> _currentHealth = new NetworkVariable<int>(
        150,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector2Int _originCoordinate;
    private int _currentRotationAngle; // 0, 90, 180, 270도
    private bool _isPlaced;
    private NavMeshObstacle _navMeshObstacle;
    private BoxCollider _antiClimbCollider;

    public Vector2Int GridSize => _gridSize;
    public string DisplayName => _displayName;
    public Vector2Int OriginCoordinate => _originCoordinate;
    public int CurrentRotationAngle => _currentRotationAngle;
    public bool IsPlaced => _isPlaced;
    public int MaxHealth => _maxHealth;
    public int CurrentHealth => _currentHealth.Value;
    public bool IsDead => _currentHealth.Value <= 0;

    public event Action<PlaceableObject> Placed;
    public event Action<PlaceableObject> Removed;
    public event Action<int, int> HealthChanged; // current, max

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _currentHealth.Value = _maxHealth;
        }

        _currentHealth.OnValueChanged += HandleHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        _currentHealth.OnValueChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int previousValue, int newValue)
    {
        HealthChanged?.Invoke(newValue, _maxHealth);
    }

    /// <summary>
    /// 지정된 회전 각도(0, 90, 180, 270도)가 적용되었을 때 차지하는 바운드 크기를 반환합니다.
    /// </summary>
    public Vector2Int GetRotatedSize(int rotationAngle)
    {
        int normalizedAngle = NormalizeRotationAngle(rotationAngle);
        if (normalizedAngle == 90 || normalizedAngle == 270)
        {
            return new Vector2Int(_gridSize.y, _gridSize.x);
        }

        return _gridSize;
    }

    /// <summary>
    /// 원점 좌표와 회전 각도를 기준으로 이 오브젝트가 점유하게 되는 모든 그리드 셀 좌표 목록을 계산하여 반환합니다.
    /// </summary>
    public List<Vector2Int> GetOccupiedCoordinates(Vector2Int originCoord, int rotationAngle)
    {
        List<Vector2Int> occupiedCoords = new List<Vector2Int>();
        Vector2Int effectiveSize = GetRotatedSize(rotationAngle);

        for (int x = 0; x < effectiveSize.x; x++)
        {
            for (int z = 0; z < effectiveSize.y; z++)
            {
                occupiedCoords.Add(new Vector2Int(originCoord.x + x, originCoord.y + z));
            }
        }

        return occupiedCoords;
    }

    /// <summary>
    /// 그리드에 성공적으로 배치되었을 때 호출됩니다.
    /// NavMeshObstacle 활성화 및 올라타기 방지 콜라이더를 세팅합니다.
    /// </summary>
    public virtual void OnPlaced(Vector2Int originCoordinate, int rotationAngle)
    {
        _originCoordinate = originCoordinate;
        _currentRotationAngle = NormalizeRotationAngle(rotationAngle);
        _isPlaced = true;

        if (_setupNavMeshObstacle)
        {
            SetupObstacle();
        }

        if (_antiClimbHeight > 0f)
        {
            SetupAntiClimbCollider();
        }

        Placed?.Invoke(this);
    }

    /// <summary>
    /// 그리드에서 제거되었을 때 호출됩니다.
    /// </summary>
    public virtual void OnRemoved()
    {
        _isPlaced = false;

        if (_navMeshObstacle != null)
        {
            _navMeshObstacle.enabled = false;
        }

        Removed?.Invoke(this);
    }

    private void SetupObstacle()
    {
        _navMeshObstacle = GetComponent<NavMeshObstacle>();
        if (_navMeshObstacle == null)
        {
            _navMeshObstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        _navMeshObstacle.carving = true;
        _navMeshObstacle.carveOnlyStationary = false;
        _navMeshObstacle.shape = NavMeshObstacleShape.Box;

        float cellSize = WorldGridManager.Instance != null ? WorldGridManager.Instance.CellSize : 1.0f;
        Vector2Int size = GetRotatedSize(_currentRotationAngle);
        _navMeshObstacle.size = new Vector3(size.x * cellSize, 2f, size.y * cellSize);
        _navMeshObstacle.center = new Vector3(0f, 1f, 0f);
        _navMeshObstacle.enabled = true;
    }

    private void SetupAntiClimbCollider()
    {
        if (_antiClimbCollider != null) return;

        // 플레이어 점프(보통 1~1.5m)를 넘어 천장 부근까지 차단벽 형성
        _antiClimbCollider = gameObject.AddComponent<BoxCollider>();
        float cellSize = WorldGridManager.Instance != null ? WorldGridManager.Instance.CellSize : 1.0f;
        Vector2Int size = GetRotatedSize(_currentRotationAngle);
        _antiClimbCollider.size = new Vector3(size.x * cellSize * 0.98f, _antiClimbHeight, size.y * cellSize * 0.98f);
        _antiClimbCollider.center = new Vector3(0f, _antiClimbHeight * 0.5f, 0f);
    }

    /// <summary>
    /// 피격 데미지 처리 (IDamageable 구현)
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        // 서버에서만 체력 연산 처리 (단일 플레이어 환경에서는 IsServer가 false일 수 있으므로 NetworkObject가 스폰되지 않았을 땐 로컬 처리)
        bool isAuthority = !IsSpawned || IsServer;
        if (!isAuthority || IsDead)
        {
            return;
        }

        // 플레이어 피격 검사: 몬스터 공격인지 플레이어 공격인지 판정
        // InstigatorClientId가 0이상이면서 플레이어인 경우
        bool isFromPlayer = damageInfo.InstigatorClientId != ulong.MaxValue;
        if (isFromPlayer && !_allowPlayerDamage)
        {
            if (damageInfo.IsCharged && _allowExplosiveDamage)
            {
                // 충전/폭발 공격은 허용 옵션이 켜져있으면 관통
            }
            else
            {
                // 일반 플레이어 공격 차단
                return;
            }
        }

        int currentHp = IsSpawned ? _currentHealth.Value : _maxHealth;
        int newHp = Mathf.Max(0, currentHp - damageInfo.Amount);

        if (IsSpawned)
        {
            _currentHealth.Value = newHp;
        }
        else
        {
            _maxHealth = newHp; // 싱글 플레이 Fallback
        }

        Debug.Log($"[PlaceableObject] '{_displayName}' 피격! 데미지: {damageInfo.Amount}, 잔여 체력: {newHp}");

        if (newHp <= 0)
        {
            DestroyByCombat(damageInfo.HitPoint);
        }
    }

    /// <summary>
    /// 공격에 의해 완전히 파괴되었을 때 처리
    /// </summary>
    private void DestroyByCombat(Vector3 hitPoint)
    {
        if (IsSpawned)
        {
            PlayDestructionClientRpc(hitPoint);
        }
        else
        {
            PlayDestructionLocal(hitPoint);
        }

        // 그리드에서 등록 해제
        if (WorldGridManager.Instance != null && _isPlaced)
        {
            WorldGridManager.Instance.TryRemoveObjectAt(_originCoordinate, out _);
        }

        // 네트워크 Despawn 또는 Destroy
        if (IsSpawned && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// F키 꾹 누름을 통해 플레이어에 의해 안전하게 철거되고 바닥에 아이템을 반환합니다.
    /// 멀티플레이어 환경에서는 ServerRpc를 통해 서버 권한으로 해체 연출, 그리드 해제, 아이템 드롭을 실행합니다.
    /// </summary>
    public void Dismantle()
    {
        if (IsSpawned)
        {
            RequestDismantleServerRpc();
        }
        else
        {
            ExecuteDismantleInternal();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDismantleServerRpc()
    {
        if (!IsServer) return;
        ExecuteDismantleInternal();
    }

    private void ExecuteDismantleInternal()
    {
        // 0. 해체 연출 브로드캐스트
        Vector3 effectPos = transform.position + Vector3.up * 0.5f;
        if (IsSpawned)
        {
            PlayDestructionClientRpc(effectPos);
        }
        else
        {
            PlayDestructionLocal(effectPos);
        }

        // 1. 그리드에서 등록 해제
        if (WorldGridManager.Instance != null && _isPlaced)
        {
            WorldGridManager.Instance.TryRemoveObjectAt(_originCoordinate, out _);
        }

        // 2. 바닥에 회수 아이템 드롭
        Vector3 dropPos = transform.position + Vector3.up * 0.5f;
        if (_droppedItemPrefab != null)
        {
            GameObject dropGO = Instantiate(_droppedItemPrefab, dropPos, Quaternion.identity);
            var netObj = dropGO.GetComponent<NetworkObject>();
            if (netObj != null && IsSpawned && IsServer)
            {
                netObj.Spawn(true);
            }
        }

        // 3. 오브젝트 제거
        if (IsSpawned && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }

        Debug.Log($"[PlaceableObject] '{_displayName}'이(가) 철거되어 아이템으로 회수되었습니다.");
    }

    [ClientRpc]
    private void PlayDestructionClientRpc(Vector3 hitPoint)
    {
        PlayDestructionLocal(hitPoint);
    }

    private void PlayDestructionLocal(Vector3 hitPoint)
    {
        if (_destructionEffectPrefab != null)
        {
            Vector3 spawnPos = hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 0.5f;
            Instantiate(_destructionEffectPrefab, spawnPos, Quaternion.identity);
        }

        // 효과음 재생
        if (_destructionSoundCue != null)
        {
            _destructionSoundCue.Play(transform.position);
        }
    }

    /// <summary>
    /// 각도를 0, 90, 180, 270 중 하나로 정규화합니다.
    /// </summary>
    public static int NormalizeRotationAngle(int angle)
    {
        int normalized = angle % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        int rounded = Mathf.RoundToInt(normalized / 90f) * 90;
        return rounded % 360;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_gridSize.x < 1) _gridSize.x = 1;
        if (_gridSize.y < 1) _gridSize.y = 1;
        if (_maxHealth < 1) _maxHealth = 1;
    }
#endif
}
