using UnityEngine;

namespace CrowdGuard.Environment
{
    /// <summary>
    /// 등반 가능한 벽면(지형) 종류
    /// </summary>
    public enum SurfaceType
    {
        BlueIce,  // 튼튼한 푸른 얼음
        WeakIce,  // 파괴될 수 있는 얕은 얼음
        Rock      // 바일이 튕겨 나가는 바위
    }

    /// <summary>
    /// 등반 지형 오브젝트(벽면)에 부착되는 컴포넌트.
    /// 아이스 바일이 이 컴포넌트를 감지하여 벽의 종류를 판별하고, 
    /// 약한 얼음의 파괴 및 낙석 이벤트를 처리합니다.
    /// </summary>
    public class ClimbableSurface : MonoBehaviour
    {
        [Header("Surface Properties")]
        public SurfaceType Type = SurfaceType.BlueIce;

        /// <summary>
        /// 아이스 바일로 벽을 타격했을 때 (트리거가 눌린 채로 충돌했을 때) 호출됩니다.
        /// 반환값(bool)이 true면 바일이 무사히 박힌 것이고, false면 얼음이 깨지거나 바위라 박히지 않은 것입니다.
        /// </summary>
        public bool OnHitByIceAxe()
        {
            if (Type == SurfaceType.Rock)
            {
                Debug.Log("[ClimbableSurface] 바위를 타격했습니다. 바일이 박히지 않고 튕깁니다.");
                // Todo: 금속 파찰음 및 스파크 이펙트 발생
                return false; 
            }

            if (Type == SurfaceType.WeakIce)
            {
                Debug.Log("[ClimbableSurface] 약한 얼음을 타격했습니다. 파괴 확률 계산 중...");
                
                // 70% 확률로 얼음 파괴
                float randomVal = Random.Range(0f, 1f);
                if (randomVal <= 0.7f)
                {
                    BreakIce();
                    
                    // 얼음이 파괴될 때 50% 확률로 낙석 트리거
                    if (Random.Range(0f, 1f) <= 0.5f) 
                    {
                        TriggerRockfall();
                    }

                    // 얼음이 깨져 없어졌으므로 바일이 벽에 박힐 수 없음 -> false 반환
                    return false;
                }
            }

            // 위 조건들을 무사히 통과했다면 (일반 푸른 얼음이거나 운좋게 약한 얼음이 안 깨졌다면) 박힘 승인!
            return true;
        }

        private void BreakIce()
        {
            Debug.Log("[ClimbableSurface] 약한 얼음이 완전히 박살났습니다!");
            // Todo: 얼음 부서지는 파티클 및 파괴 사운드 재생
            Destroy(gameObject); // 객체 삭제
        }

        private void TriggerRockfall()
        {
            Debug.Log("[ClimbableSurface] 낙석 주의");
            // 추후 Network 매니저(서버 담당자 파트)나 방해 요소 매니저에게 낙석 소환 이벤트를 던집니다.
        }
    }
}
