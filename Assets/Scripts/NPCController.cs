using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 서버 권한(Server-Authoritative) 기반의 NPC 컨트롤러입니다.
/// IInteractable 인터페이스를 구현하여 플레이어가 다가가 E키를 눌러 대화하거나 상호작용할 수 있습니다.
///
/// 호스트 환경에서도 NPC는 플레이어 소유가 아니며(HasPlayerOwner == false),
/// 호스트 플레이어의 입력/시점과 무관하게 서버 주도형 객체로 동작합니다.
/// </summary>
public class NPCController : NonPlayerCharacter, IInteractable
{
    [Header("NPC Interaction Settings")]
    [SerializeField] private string _interactionPrompt = "대화하기";
    [SerializeField] private float _interactionRange = 4.0f;
    [SerializeField] private float _interactionDuration = 4.0f;
    [SerializeField] private float _turnSpeed = 5.0f;

    [Header("Dialog Data")]
    [SerializeField] private string[] _dialogLines = new string[]
    {
        "안녕하세요, 모험가님! 도움이 필요하신가요?",
        "이 주변에는 위험한 몬스터들이 출몰하니 조심하세요.",
        "서버에서 안전하게 관리되고 있는 마을입니다."
    };

    [Header("Movement / Idle Settings")]
    [SerializeField] private bool _canWander = false;
    [SerializeField] private float _wanderRadius = 6.0f;
    [SerializeField] private float _wanderInterval = 5.0f;

    private NavMeshAgent _navMeshAgent;
    private Transform _currentInteractingPlayer;
    private float _interactionTimer;
    private float _wanderTimer;
    private Vector3 _initialPosition;
    private int _dialogIndex;
    private Renderer[] _highlightRenderers;

    protected override void Awake()
    {
        base.Awake();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _highlightRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _initialPosition = transform.position;
            _wanderTimer = _wanderInterval;
        }
        else
        {
            // 클라이언트 측에서는 자체 내비게이션 비활성화 (NetworkTransform 동기화 사용)
            if (_navMeshAgent != null)
            {
                _navMeshAgent.enabled = false;
            }
        }
    }

    private void Update()
    {
        // [중요] 오직 서버에서만 NPC의 행동 및 상태 전이를 연산합니다.
        if (!IsServer || IsDead)
        {
            return;
        }

        if (CurrentState == CharacterState.Interacting)
        {
            HandleInteractingState();
        }
        else if (_canWander)
        {
            HandleWanderingState();
        }
    }

    private void HandleInteractingState()
    {
        // 상호작용한 플레이어를 부드럽게 바라봄
        if (_currentInteractingPlayer != null)
        {
            Vector3 direction = _currentInteractingPlayer.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
            }
        }

        _interactionTimer -= Time.deltaTime;
        if (_interactionTimer <= 0f)
        {
            _currentInteractingPlayer = null;
            SetState(CharacterState.Idle);
        }
    }

    private void HandleWanderingState()
    {
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer = _wanderInterval;
            WanderToRandomPoint();
        }
    }

    private void WanderToRandomPoint()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _wanderRadius;
        Vector3 target = _initialPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.SetDestination(target);
            SetState(CharacterState.Patrol);
        }
    }

    #region IInteractable Implementation

    public Renderer[] GetHighlightRenderers()
    {
        if (_highlightRenderers == null || _highlightRenderers.Length == 0)
        {
            _highlightRenderers = GetComponentsInChildren<Renderer>(true);
        }
        return _highlightRenderers;
    }

    public string GetInteractionPrompt()
    {
        return _interactionPrompt;
    }

    public bool CanInteract(PlayerInteraction interactor)
    {
        if (IsDead || interactor == null)
        {
            return false;
        }

        float distance = Vector3.Distance(transform.position, interactor.transform.position);
        return distance <= _interactionRange;
    }

    public void Interact(PlayerInteraction interactor)
    {
        if (interactor == null)
        {
            return;
        }

        // 플레이어 클라이언트의 요청을 서버로 전달
        RequestInteractServerRpc(interactor.NetworkObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(ulong interactorNetworkObjectId)
    {
        if (!IsServer || IsDead)
        {
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(interactorNetworkObjectId, out NetworkObject interactorNetObj))
        {
            _currentInteractingPlayer = interactorNetObj.transform;
            _interactionTimer = _interactionDuration;
            SetState(CharacterState.Interacting);

            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = true;
            }

            // 대화 대사 선택
            string line = GetNextDialogLine();
            NotifyDialogClientRpc(line, interactorNetObj.OwnerClientId);
        }
    }

    private string GetNextDialogLine()
    {
        if (_dialogLines == null || _dialogLines.Length == 0)
        {
            return "...";
        }

        string line = _dialogLines[_dialogIndex];
        _dialogIndex = (_dialogIndex + 1) % _dialogLines.Length;
        return line;
    }

    [ClientRpc]
    private void NotifyDialogClientRpc(string dialogText, ulong targetClientId)
    {
        Debug.Log($"[NPCController] [{CharacterName}]: {dialogText}");

        // 만약 로컬 플레이어가 상호작용한 대상이라면 화면 또는 UI에 대화 출력
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            // 상호작용 UI 연동 포인트
            Debug.Log($"[NPC 대화 수신] {CharacterName}: {dialogText}");
        }
    }

    #endregion

    protected override void HandleDeath(DamageInfo lastDamage)
    {
        base.HandleDeath(lastDamage);
        _currentInteractingPlayer = null;

        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }
    }
}
