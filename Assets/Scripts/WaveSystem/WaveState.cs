public enum WaveState
{
    Preparation, // 파밍 및 빌딩 페이즈
    Warning,     // 사이렌 및 웨이브 시작 대기
    Combat,      // 적 스폰 및 디펜스
    Ending       // 적 전멸 대기 및 웨이브 종료 처리
}
