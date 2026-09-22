namespace Coop.VFX
{
    /// <summary>
    /// 타격 지점의 표면 및 재질 유형을 정의하는 열거형입니다.
    /// 멀티플레이 RPC 전송 시 1바이트(byte)로 직렬화되어 대역폭을 최소화합니다.
    /// </summary>
    public enum SurfaceType : byte
    {
        Default = 0,    // 기본 표면 / 환경
        Stone = 1,      // 콘크리트, 돌, 벽돌 (먼지/돌가루)
        Flesh = 2,      // 몬스터, 생체 조직 (혈흔/살점)
        Metal = 3,      // 금속, 철판, 기계 (스파크)
        Wood = 4        // 목재, 가구, 나무 판자 (나무 파편)
    }
}
