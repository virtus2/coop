using UnityEngine;

/// <summary>
/// 횃불/조명 느낌의 레트로 포인트 라이트 깜빡임 연출 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Light))]
public class RetroTorchFlicker : MonoBehaviour
{
    [SerializeField] private float _baseIntensity = 2.0f;
    [SerializeField] private float _flickerRange = 0.5f;
    [SerializeField] private float _flickerSpeed = 8.0f;
    [SerializeField] private bool _useSteppedFlicker = true;
    [SerializeField] private int _stepCount = 5;

    private Light _lightComponent;
    private float _noiseOffset;

    private void Awake()
    {
        _lightComponent = GetComponent<Light>();
        _noiseOffset = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (_lightComponent == null)
        {
            return;
        }

        float noise = Mathf.PerlinNoise(_noiseOffset, Time.time * _flickerSpeed);
        float offset = (noise - 0.5f) * 2f * _flickerRange;

        if (_useSteppedFlicker && _stepCount > 0)
        {
            offset = Mathf.Round(offset * _stepCount) / _stepCount;
        }

        _lightComponent.intensity = Mathf.Max(0.1f, _baseIntensity + offset);
    }
}
