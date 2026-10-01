using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ?�버 권한(Server-Authoritative) 기반??몬스??AI 컨트롤러?�니??
/// 모든 AI ?�사결정(?�색, ?�찰, 추적, 공격) �??�동?� ?�직 ?�버(IsServer)?�서�??�행?�며,
/// ?�라?�언?�는 NetworkTransform �?NetworkVariable???�해 ?�기?�된 결과�??�더링합?�다.
///
/// ?�스???�경?�서??몬스?�는 ?�레?�어 ?�유가 ?�니�?HasPlayerOwner == false),
/// ?�스?�의 ?�레?�어 ?�력/?�점 ?�스?�과 ?�전??격리?�어 ?�립?�으�??�작?�니??
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
            // ?�라?�언??측에?�는 NavMeshAgent가 ?�체 물리/?�동???�리지 ?�도�?비활?�화
            // (?�기?�는 ?�직 NetworkTransform???�당)
            if (_navMeshAgent != null)
            {
                _navMeshAgent.enabled = false;
            }
        }
    }

    private void Update()
    {
        // [중요] ?�직 ?�버?�서�?AI 로직�??�동???�어?�니??
        // ?�스???�경?�라??IsServer가 ?�닌 ?�라?�언??관?�에?�는 AI가 ?�행?��? ?�으�?
        // ?�격 ?�라?�언????�� ?�버가 ?�기?�해주는 Transform�??�태�??�신?�니??
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
        // 1. ?�레?�어 감�? ?�인
        CheckForPlayers();

        // 2. ?�재 ?�태�?로직 ?�행
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
            // ?�찰 목표 지??무작???�성 ??Patrol ?�태�??�환
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

        // 추적 범위 벗어??        if (distanceToTarget > _loseTargetRadius)
        {
            _currentTargetPlayer = null;
            SetState(CharacterState.Idle);
            return;
        }

        // 공격 ?�거�??�달
        if (distanceToTarget <= _attackRange)
        {
            StopMovement();
            SetState(CharacterState.Attack);
            return;
        }

        // ?�겟을 ?�해 ?�동
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
            // ?�겟이 공격 ?�거�?밖으�??�망�?-> ?�시 추적
            SetState(CharacterState.Chase);
            return;
        }

        // ?��?방향?�로 부?�럽�??�전
        RotateTowards(_currentTargetPlayer.position);

        // 공격 ?�행
        if (_attackTimer <= 0f)
        {
            ExecuteAttack();
            _attackTimer = _attackCooldown;
        }
    }

    private void ExecuteAttack()
    {
        // ?�라?�언?�에 공격 ?�니메이???�펙???�리�?        PlayAttackEffectClientRpc();

        // ?�겟에�??��?지 ?�정
        if (_currentTargetPlayer != null)
        {
            var core = _currentTargetPlayer.GetComponent<ContainmentCore>();
            if (core != null)
            {
                core.TakeDamage(_attackDamage);
                Debug.Log($"[MonsterController] 몬스??'{name}'가 코어?�게 {_attackDamage} ?��?지�?가??");
            }
            else
            {
                Debug.Log($"[MonsterController] 몬스??'{name}'가 ?�레?�어 '{_currentTargetPlayer.name}'?�게 {_attackDamage}??공격??가??");
                // ?�레?�어??체력/?�격 ?�터?�이???�는 컴포?�트가 ?�다�??�기???�용 가??            
            }
        }
    }

    [ClientRpc]
    private void PlayAttackEffectClientRpc()
    {
        // ?�라?�언???�스???�함)?�서 공격 ?�운?? ?�펙?? ?�니메이???�생
    }

    #endregion

    #region AI Perception & Navigation (Server Only)

    private void CheckForPlayers()
    {
        if (_currentTargetPlayer != null)
        {
            return; // ?��? ?�겟이 ?�으�?HandleChase/Attack?�서 거리 ?�단
        }

        // 1. 반경 ???�레?�어 ?�색 (?�선?�위)
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
        // 2. 주�????��?가 ?�다�?코어�?최우???�겟으�?지??        
        else if (ContainmentCore.Instance != null && ContainmentCore.Instance.CurrentHealth.Value > 0)
        {
            _currentTargetPlayer = ContainmentCore.Instance.transform;
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

        // NavMeshAgent가 ?�용 가?�한 경우 ?�선 ?�용
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.SetDestination(destination);
            return;
        }

        // NavMesh가 ?�는 ?�경???�한 Fallback 직접 ?�동 (CharacterController ?�는 Transform)
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

    [Header("Knockback Settings")]
    [Tooltip("?�백 ?�??��. 값이 ?�수�???밀?�나�? 1?� 기본, Mathf.Infinity???�백 면역?�니??")]
    [SerializeField] private float _knockbackResistance = 1.0f;

    private Vector3 _knockbackVelocity;

    protected override void ApplyKnockback(Vector3 direction, float force)
    {
        if (!IsServer || IsDead) return;

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f && _knockbackResistance > 0f && !float.IsInfinity(_knockbackResistance))
        {
            direction.Normalize();
            float effectiveForce = force / _knockbackResistance;
            _knockbackVelocity += direction * effectiveForce;
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

        // 지??감쇠 (마찰???�용)
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

    protected override void HandleDeath(DamageInfo lastDamage)
    {
        base.HandleDeath(lastDamage);
        StopMovement();

        if (_navMeshAgent != null)
        {
            _navMeshAgent.enabled = false;
        }

        if (_characterController != null)
        {
            _characterController.enabled = false;
        }
    }

    protected override void OnClientDied(Vector3 knockbackDir, float knockbackForce, Vector3 hitPoint)
    {
        base.OnClientDied(knockbackDir, knockbackForce, hitPoint);
        
        var ragdoll = GetComponent<RagdollController>();
        if (ragdoll != null)
        {
            float force = knockbackForce * 2f; // ��Ÿ �˹��� �� �� ����
            if (force < 10f) force = 10f; // �ּ� ���ư��� �� ����
            
            ragdoll.ApplyForceToRagdoll(knockbackDir * force, hitPoint);
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
