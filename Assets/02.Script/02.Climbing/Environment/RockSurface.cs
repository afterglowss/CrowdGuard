using UnityEngine;
using Fusion;

namespace CrowdGuard.Environment
{
    public class RockSurface : BaseSurface
    {
        public override bool OnHitByIceAxe()
        {
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position);
            }
            // 바위는 무조건 튕겨 나감
            Debug.Log("[RockSurface] 바위 표면입니다. 바일이 튕겨 나갑니다!");
            return false;
        }

        public override bool CanInstallAnchor()
        {
            return false; // 바위에는 앵커 고정 불가능
        }
    }
}
