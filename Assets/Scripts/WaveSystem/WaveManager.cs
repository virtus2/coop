using UnityEngine;
using Unity.Netcode;
using System;

public class WaveManager : NetworkBehaviour
{
    public static WaveManager Instance { get; private set; }

    public NetworkVariable<WaveState> CurrentState = new NetworkVariable<WaveState>(WaveState.Preparation);
    public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(180f);
    public NetworkVariable<int> CurrentWave = new NetworkVariable<int>(1);

    [Header("Phase Durations (Configurable)")]
    public float preparationDuration = 180f; // 3분
    public float warningDuration = 10f;      // 10초
    public float combatDuration = 120f;      // 2분

    [Header("Dependencies")]
    [SerializeField] private EnemySpawner enemySpawner;
    
    public Action<WaveState> OnWaveStateChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartPhase(WaveState.Preparation);
        }

        CurrentState.OnValueChanged += (oldState, newState) => 
        {
            OnWaveStateChanged?.Invoke(newState);
            // TODO: 경보 상태(Warning)일 때 비상 사이렌 소리 및 붉은 조명 연출 추가
        };
    }

    private void Update()
    {
        if (!IsServer) return;

        // Ending 페이즈가 아니면 타이머 진행
        if (CurrentState.Value != WaveState.Ending)
        {
            TimeRemaining.Value -= Time.deltaTime;

            if (TimeRemaining.Value <= 0)
            {
                TransitionToNextPhase();
            }
        }
        else
        {
            // Ending 페이즈: 남은 적이 모두 죽을 때까지 대기
            if (enemySpawner != null && enemySpawner.ActiveEnemyCount <= 0)
            {
                // 웨이브 클리어 성공
                CurrentWave.Value++;
                StartPhase(WaveState.Preparation);
            }
        }
    }

    private void TransitionToNextPhase()
    {
        switch (CurrentState.Value)
        {
            case WaveState.Preparation:
                StartPhase(WaveState.Warning);
                break;
            case WaveState.Warning:
                StartPhase(WaveState.Combat);
                break;
            case WaveState.Combat:
                StartPhase(WaveState.Ending);
                break;
        }
    }

    private void StartPhase(WaveState state)
    {
        CurrentState.Value = state;
        
        switch (state)
        {
            case WaveState.Preparation:
                TimeRemaining.Value = preparationDuration;
                if (enemySpawner != null) enemySpawner.StopSpawning();
                Debug.Log($"[WaveManager] 빌딩 페이즈 시작 (웨이브 {CurrentWave.Value})");
                break;
                
            case WaveState.Warning:
                TimeRemaining.Value = warningDuration;
                Debug.Log("[WaveManager] 경보 페이즈 시작! 적들이 몰려옵니다.");
                break;
                
            case WaveState.Combat:
                TimeRemaining.Value = combatDuration;
                if (enemySpawner != null) enemySpawner.StartSpawning(CurrentWave.Value);
                Debug.Log("[WaveManager] 디펜스 페이즈 시작!");
                break;
                
            case WaveState.Ending:
                TimeRemaining.Value = 0f;
                if (enemySpawner != null) enemySpawner.StopSpawning();
                Debug.Log("[WaveManager] 적 스폰 종료. 잔당을 처리하세요.");
                break;
        }
    }
}
