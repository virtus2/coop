using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 서버 권한(Server-Authoritative) 기반의 몬스터 AI 컨트롤러입니다.
/// 모든 AI 의사결정(탐색, 순찰, 추적, 공격) 및 이동은 오직 서버(IsServer)에서만 실행되며,
/// 클라이언트는 NetworkTransform 및 NetworkVariable을 통해 동기화된 결과를 렌더링합니다.
///
/// 호스트 환경에서도 몬스터는 플레이어 소유가 아니며(HasPlayerOwner == false),
/// 호스트의 플레이어 입력/시점 시스템과 완전히 격리되어 독립적으로 동작합니다.
/// </summary>
public class MonsterController : NonPlayerCharacter
{
    [Header("Monster Settings")]
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private float _chaseSpeed = 5.0f;
    [SerializeField] private float _rotationSpeed = 8.0f;

    [Header("AI Perception")]
    [SerializeField] private float _detectionRadius = 12f;
    [SerializeField] private float _loseTargetRadius = 18f;
    [SerializeField] private float _attackRange = 1.8f;
    [SerializeField] private LayerMask _playerLayerMask = ~0;

    [Header("AI Combat")]
    [SerializeField] private int _attackDamage = 15;
    [SerializeField] private float _attackCooldown = 1.5f;

    [Header("Patrol Settings")]
    [SerializeField] private float _patrolRadius = 15f;
    [SerializeField] private float _idleDuration = 2.5f;

    private NavMeshAgent _navMeshAgent;
    private CharacterController _characterController;

    private Vector3 _spawnPosition;
    private Vector3 _patrolTarget;
    private Transform _currentTargetPlayer;
    private float _idleTimer;
    private float _attackTimer;
    private readonly Collider[] _detectionBuffer = new Collider[16];

    protected override void Awake()
    {
        base.Awake();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _spawnPosition = transform.position;
            _patrolTarget = _spawnPosition;
            _idleTimer = _idleDuration;

            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.speed = _moveSpeed;
            }
        }
        else
        {
            // 클라이언트 측에서는 NavMeshAgent가 자체 물리/이동을 돌리지 않도록 비활성화
            // (동기화는 오직 NetworkTransform이 담당)
            if (_navMeshAgent != null)
            {
                _navMeshAgent.enabled = false;
            }
        }
    }

    private void Update()
    {
        // [중요] 오직 서버에서만 AI 로직과 이동을 제어합니다.
        // 호스트 환경이라도 IsServer가 아닌 클라이언트 관점에서는 AI가 실행되지 않으며,
        // 원격 클라이언트 역시 서버가 동기화해주는 Transform과 상태만 수신합니다.
        if (!IsServer || IsDead)
        {
            return;
        }

        UpdateStagger(Time.deltaTime);
        UpdateKnockback(Time.deltaTime);

        if (IsStaggered)
        {
            StopMovement();
            return;
        }

        UpdateCooldowns();
        UpdateStateMachine();
    }

    private void UpdateCooldowns()
    {
        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;
        }
    }

    private void UpdateStateMachine()
    {
        // 1. 플레이어 감지 확인
        CheckForPlayers();

        // 2. 현재 상태별 로직 실행
        switch (CurrentState)
        {
            case CharacterState.Idle:
                HandleIdleState();
                break;

            case CharacterState.Patrol:
                HandlePatrolState();
                break;

            case CharacterState.Chase:
                HandleChaseState();
                break;

            case CharacterState.Attack:
                HandleAttackState();
                break;
        }
    }

    #region AI State Handlers (Server Only)

    private void HandleIdleState()
    {
        StopMovement();

        _idleTimer -= Time.deltaTime;
        if (_idleTimer <= 0f)
        {
            // 순찰 목표 지점 무작위 생성 후 Patrol 상태로 전환
            _patrolTarget = GetRandomPatrolPoint();
            _idleTimer = _idleDuration;
            SetState(CharacterState.Patrol);
        }
    }

    private void HandlePatrolState()
    {
        SetMovementSpeed(_moveSpeed);
        MoveTowards(_patrolTarget);

        float distanceToTarget = Vector3.Distance(transform.position, _patrolTarget);
        if (distanceToTarget <= 1.2f)
        {
            StopMovement();
            SetState(CharacterState.Idle);
        }
    }

    private void HandleChaseState()
    {
        if (_currentTargetPlayer == null)
        {
            SetState(CharacterState.Idle);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, _currentTargetPlayer.position);

        // 추적 범위 벗어남
        if (distanceToTarget > _loseTargetRadius)
        {
            _currentTargetPlayer = null;
            SetState(CharacterState.Idle);
            return;
        }

        // 공격 사거리 도달
        if (distanceToTarget <= _attackRange)
        {
            StopMovement();
            SetState(CharacterState.Attack);
            return;
        }

        // 타겟을 향해 이동
        SetMovementSpeed(_chaseSpeed);
        MoveTowards(_currentTargetPlayer.position);
    }

    private void HandleAttackState()
    {
        if (_currentTargetPlayer == null)
        {
            SetState(CharacterState.Idle);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, _currentTargetPlayer.position);
        if (distanceToTarget > _attackRange * 1.3f)
        {
            // 타겟이 공격 사거리 밖으로 도망침 -> 다시 추적
            SetState(CharacterState.Chase);
            return;
        }

        // 타겟 방향으로 부드럽게 회전
        RotateTowards(_currentTargetPlayer.position);

        // 공격 실행
        if (_attackTimer <= 0f)
        {
            ExecuteAttack();
            _attackTimer = _attackCooldown;
        }
    }

    private void ExecuteAttack()
    {
        // 클라이언트에 공격 애니메이션/이펙트 트리거
        PlayAttackEffectClientRpc();

        // 타겟 플레이어에게 데미지 판정
        if (_currentTargetPlayer != null)
        {
            Debug.Log($"[MonsterController] 몬스터 '{name}'가 플레이어 '{_currentTargetPlayer.name}'에게 {_attackDamage}의 공격을 가함!");
            // 플레이어에 체력/피격 인터페이스 또는 컴포넌트가 있다면 여기서 적용 가능
        }
    }

    [ClientRpc]
    private void PlayAttackEffectClientRpc()
    {
        // 클라이언트(호스트 포함)에서 공격 사운드, 이펙트, 애니메이션 재생
    }

    #endregion

    #region AI Perception & Navigation (Server Only)

    private void CheckForPlayers()
    {
        if (_currentTargetPlayer != null)
        {
            return; // 이미 타겟이 있으면 HandleChase/Attack에서 거리 판단
        }

        // 반경 내 플레이어 탐색
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _detectionRadius, _detectionBuffer, _playerLayerMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        Transform closestPlayer = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _detectionBuffer[i];
            PlayerController player = col.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestPlayer = player.transform;
                }
            }
        }

        if (closestPlayer != null)
        {
            _currentTargetPlayer = closestPlayer;
            SetState(CharacterState.Chase);
        }
    }

    private Vector3 GetRandomPatrolPoint()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _patrolRadius;
        Vector3 target = _spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return target;
    }

    private void SetMovementSpeed(float speed)
    {
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.speed = speed;
        }
    }

    private void MoveTowards(Vector3 destination)
    {
        RotateTowards(destination);

        // NavMeshAgent가 사용 가능한 경우 우선 활용
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.SetDestination(destination);
            return;
        }

        // NavMesh가 없는 환경을 위한 Fallback 직접 이동 (CharacterController 또는 Transform)
        Vector3 direction = (destination - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            Vector3 moveDelta = direction.normalized * (_moveSpeed * Time.deltaTime);

            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(moveDelta + Vector3.down * 9.81f * Time.deltaTime);
            }
            else
            {
                transform.position += moveDelta;
            }
        }
    }

    private Vector3 _knockbackVelocity;

    protected override void ApplyKnockback(Vector3 direction, float force)
    {
        if (!IsServer || IsDead) return;

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();
            _knockbackVelocity = direction * force;
        }
    }

    private void UpdateKnockback(float deltaTime)
    {
        if (_knockbackVelocity.sqrMagnitude <= 0.01f) return;

        Vector3 moveDelta = _knockbackVelocity * deltaTime;
        if (_characterController != null && _characterController.enabled)
        {
            _characterController.Move(moveDelta);
        }
        else if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.Move(moveDelta);
        }
        else
        {
            transform.position += moveDelta;
        }

        // 지수 감쇠 (마찰력 적용)
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, 12f * deltaTime);
    }

    private void StopMovement()
    {
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }

    #endregion

    protected override void HandleDeath()
    {
        base.HandleDeath();
        StopMovement();

        if (_navMeshAgent != null)
        {
            _navMeshAgent.enabled = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? _spawnPosition : transform.position;
        Gizmos.DrawWireSphere(center, _patrolRadius);
    }
#endif
}
