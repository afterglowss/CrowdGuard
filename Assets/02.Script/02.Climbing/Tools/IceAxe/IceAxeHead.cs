using UnityEngine;
using CrowdGuard.Environment;

namespace CrowdGuard.Climbing.Tools.IceAxe
{
    /// <summary>
    /// 아이스 바일의 머리(찍는 부분) 전용 센서 스크립트.
    /// Physics.OverlapSphere를 사용하므로 벽 MeshCollider의 Convex 여부에 관계없이 동작합니다.
    ///
    /// [기존 OnTriggerEnter 방식 제거 이유]
    /// Non-Convex MeshCollider를 isTrigger=true로 설정할 수 없는 Unity 제약으로 인해
    /// 벽 형태가 복잡한 맵에서 Convex 강제 설정이 필요했습니다.
    /// OverlapSphere는 Static Non-Convex MeshCollider도 완전히 감지합니다.
    ///
    /// [씬 설정 주의]
    /// - 이 오브젝트의 SphereCollider isTrigger 체크 해제 가능 (더 이상 사용 안 함)
    /// - Surface 레이어 마스크(_surfaceLayerMask)를 벽 레이어로 좁히면 성능 향상
    /// </summary>
    public class IceAxeHead : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("아이스 바일 몸체에 달린 컨트롤러를 연결해 주세요.")]
        [SerializeField] private IceAxeController _controller;

        [Header("Detection")]
        [Tooltip("감지 반경 (m). 기존 SphereCollider 반경과 동일하게 맞추세요.")]
        [SerializeField] private float _detectionRadius = 0.05f;

        [Tooltip("감지할 레이어 마스크. Wall / Surface 레이어만 포함하면 성능에 유리합니다.")]
        [SerializeField] private LayerMask _surfaceLayerMask = ~0;

        private BaseSurface _currentSurface;
        private bool _isTouching;

        private void FixedUpdate()
        {
            if (_controller == null) return;

            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                _detectionRadius,
                _surfaceLayerMask,
                QueryTriggerInteraction.Ignore);

            // 범위 내에서 BaseSurface를 가진 콜라이더 탐색
            BaseSurface found = null;
            foreach (var col in hits)
            {
                found = col.GetComponentInParent<BaseSurface>();
                if (found != null) break;
            }

            if (found != null && !_isTouching)
            {
                // 새로 접촉
                _isTouching = true;
                _currentSurface = found;
                _controller.OnIceContactEnter(found);
            }
            else if (found == null && _isTouching)
            {
                // 접촉 종료
                var prev = _currentSurface;
                _isTouching = false;
                _currentSurface = null;
                _controller.OnIceContactExit(prev);
            }
            else if (found != null && _isTouching && found != _currentSurface)
            {
                // 다른 Surface로 전환 (예: 두 벽면의 경계)
                _controller.OnIceContactExit(_currentSurface);
                _currentSurface = found;
                _controller.OnIceContactEnter(found);
            }
        }
    }
}
