using UnityEngine;

namespace CrowdGuard.XR.Haptics
{
    /// <summary>
    /// 상황별 햅틱 프로파일 타입
    /// </summary>
    public enum HapticType
    {
        None = 0,
        
        // Impact — 순간 충격 (바일 벽 타격, 앵커 삽입)
        IceAxeAttach,
        AnchorInsert,
        AnchorSecured,

        // Interaction — 잡기/놓기, 패널 터치 등
        GrabHover,
        GrabSelect,

        // 향후 확장용 (RopeTension, Blizzard 등)
        RopeTension,
        Blizzard,
        SensorDirection
    }
}