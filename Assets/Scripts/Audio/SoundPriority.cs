namespace Coop.Audio
{
    /// <summary>
    /// 효과음 재생 시 풀(Pool)이 가득 찼을 때 선점(Preemption) 여부를 결정하는 우선순위입니다.
    /// 높은 수치일수록 높은 우선순위를 가지며, 낮은 우선순위의 사운드를 중단시키고 재생될 수 있습니다.
    /// </summary>
    public enum SoundPriority
    {
        Low = 0,
        Normal = 10,
        High = 20,
        Critical = 30
    }
}
