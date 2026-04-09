namespace CrowdGuard.XR.Haptics
{
    /// <summary>
    /// 컨트롤러에 부착되어 런타임에 진동을 발생시키는 프로바이더 인터페이스.
    /// 중앙 매니저 없이 도구가 각 컨트롤러 환경(HapticProvider)을 찾아 직접 파라미터를 넘깁니다.
    /// </summary>
    public interface IHapticProvider
    {
        /// <summary>
        /// 프로파일(Meta Clip 우선, 없음 XRI 폴백)을 기반으로 진동 재생
        /// </summary>
        void PlayHaptic(HapticProfile profile);

        /// <summary>
        /// XRI Fallback 용도의 수동 진동 재생
        /// </summary>
        void PlayHaptic(float amplitude, float duration);
    }
}
