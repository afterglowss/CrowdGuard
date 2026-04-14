using UnityEngine;
using Fusion;

namespace CrowdGuard.Environment
{
    public class BlueIceSurface : BaseSurface
    {
        public override bool OnHitByIceAxe()
        {
            if (GetIsBrokenSafe()) return false;
            // 튼튼하므로 무사히 박힘 허용
            return true;
        }

        public override bool CanInstallAnchor()
        {
            if (GetIsBrokenSafe()) return false;
            return true; // 앵커 고정 지원
        }
    }
}
