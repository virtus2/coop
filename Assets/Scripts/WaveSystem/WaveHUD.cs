using UnityEngine;
using TMPro; // TextMeshPro를 사용한다고 가정

public class WaveHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI wavePhaseText;
    [SerializeField] private TextMeshProUGUI timerText;

    private void Update()
    {
        if (WaveManager.Instance == null) return;

        WaveState currentState = WaveManager.Instance.CurrentState.Value;
        float timeRemaining = WaveManager.Instance.TimeRemaining.Value;

        // 페이즈 텍스트 업데이트
        switch (currentState)
        {
            case WaveState.Preparation:
                wavePhaseText.text = $"BUILDING PHASE (WAVE {WaveManager.Instance.CurrentWave.Value})";
                wavePhaseText.color = Color.white;
                break;
            case WaveState.Warning:
                wavePhaseText.text = "WARNING: CONTAINMENT BREACH INCOMING";
                wavePhaseText.color = Color.yellow;
                break;
            case WaveState.Combat:
                wavePhaseText.text = "DEFEND THE CORE!";
                wavePhaseText.color = Color.red;
                break;
            case WaveState.Ending:
                wavePhaseText.text = "ELIMINATE REMAINING ENEMIES";
                wavePhaseText.color = Color.green;
                break;
        }

        // 타이머 텍스트 업데이트 (Ending 페이즈에는 타이머 숨김 또는 00:00 표시)
        if (currentState == WaveState.Ending)
        {
            timerText.text = "--:--";
        }
        else
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
