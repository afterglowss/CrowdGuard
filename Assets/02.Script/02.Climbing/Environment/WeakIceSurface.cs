using UnityEngine;
using Fusion;

namespace CrowdGuard.Environment
{
    public class WeakIceSurface : BaseSurface
    {
        [Header("Weak Ice Settings")]
        [SerializeField, Range(0f, 1f)] private float breakProbability = 0.8f;

        public override bool OnHitByIceAxe()
        {
            if (GetIsBrokenSafe()) return false;

            // 임시 클라이언트 주도 파괴 처리 (실제로는 RPC로 권한 이전받거나 호스트 위임 필요)
            float roll = Random.Range(0f, 1f);
            if (roll <= breakProbability)
            {
                Debug.Log("[WeakIceSurface] 약한 얼음 파괴 확률에 당첨되어 파괴됩니다!");
                SetBrokenSafe(true);

                // 기획: 얼음 파괴시 50% 확률로 낙석 트리거 시그널 발송
                if (Random.Range(0f, 1f) <= 0.5f)
                {
                    HazardData rockfallData = new RockfallData { Location = transform.position, RockCount = 3, FallRadius = 2f };
                    HazardManager.Instance?.TriggerHazardExternal(rockfallData);
                }

                return false; // 파괴 시 박히지 않음
            }

            return true; // 운 좋게 파괴 안됨
        }

        public override bool CanInstallAnchor()
        {
            if (GetIsBrokenSafe()) return false;
            return true; // 간당간당하지만 박을 순 있음
        }
    }
}
