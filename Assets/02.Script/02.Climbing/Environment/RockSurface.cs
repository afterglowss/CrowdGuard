using Fusion;
using SimpleAudioManager;
using UnityEngine;

namespace CrowdGuard.Environment
{
    public class RockSurface : BaseSurface
    {
        public override bool OnHitByIceAxe(Vector3 contactPoint = default)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFXType.PickRock, transform);
            // 바위는 무조건 튕겨 나감
            Debug.Log("[RockSurface] 바위 표면입니다. 바일이 튕겨 나갑니다!");
            return false;
        }

        public override bool CanInstallAnchor(Vector3 contactPoint = default)
        {
            return false; // 바위에는 앵커 고정 불가능
        }
    }
}
