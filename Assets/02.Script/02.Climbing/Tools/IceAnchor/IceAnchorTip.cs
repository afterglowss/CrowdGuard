using UnityEngine;
using CrowdGuard.Environment;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// 아이스 앵커의 끝(삽입 부분) 전용 센서 스크립트.
    /// Physics.OverlapSphere를 사용하므로 벽 MeshCollider의 Convex 여부에 관계없이 동작합니다.
    ///
    /// [기존 OnTriggerEnter 방식 제거 이유]
    /// IceAxeHead와 동일 — Non-Convex MeshCollider Trigger 제약 회피.
    ///
    /// [씬 설정 주의]
    /// - Surface 레이어 마스크(_surfaceLayerMask)를 벽 레이어로 좁히면 성능 향상
    /// </summary>
    public class IceAnchorTip : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("루트의 IceAnchorController를 연결해 주세요.")]
        [SerializeField] private IceAnchorController _controller;

        [Header("Detection")]
        [Tooltip("감지 반경 (m).")]
        [SerializeField] private float _detectionRadius = 0.03f;

        [Tooltip("감지할 레이어 마스크. Wall / Surface 레이어만 포함하면 성능에 유리합니다.")]
        [SerializeField] private LayerMask _surfaceLayerMask = ~0;

        private bool _isTouching;
        private BaseSurface    _trackedBase;
#pragma warning disable CS0618
        private ClimbableSurface _trackedLegacy;
#pragma warning restore CS0618

        private void FixedUpdate()
        {
            if (_controller == null) return;

            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                _detectionRadius,
                _surfaceLayerMask,
                QueryTriggerInteraction.Ignore);

            // BaseSurface 우선, ClimbableSurface 폴백
            BaseSurface foundBase = null;
#pragma warning disable CS0618
            ClimbableSurface foundLegacy = null;
#pragma warning restore CS0618
            Collider hitCollider = null;

            foreach (var col in hits)
            {
                foundBase = col.GetComponentInParent<BaseSurface>();
                if (foundBase != null) { hitCollider = col; break; }

#pragma warning disable CS0618
                foundLegacy = col.GetComponentInParent<ClimbableSurface>();
#pragma warning restore CS0618
                if (foundLegacy != null) { hitCollider = col; break; }
            }

            bool anyFound = foundBase != null || foundLegacy != null;

            if (anyFound && !_isTouching)
            {
                // 접촉 시작 — 법선 계산 후 Controller로 전달
                _isTouching = true;

                Vector3 cp  = hitCollider.ClosestPoint(transform.position);
                Vector3 dir = transform.position - cp;
                // ClosestPoint가 팁 위치와 겹치면(표면 내부) 팁의 -forward를 fallback으로 사용
                Vector3 normal = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

                if (foundBase != null)
                {
                    _trackedBase = foundBase;
                    _controller.OnWallContactEnter(foundBase, cp, normal);
                }
                else
                {
                    _trackedLegacy = foundLegacy;
#pragma warning disable CS0618
                    _controller.OnWallContactEnterLegacy(foundLegacy, cp, normal);
#pragma warning restore CS0618
                }
            }
            else if (!anyFound && _isTouching)
            {
                // 접촉 종료
                _isTouching = false;

                if (_trackedBase != null)
                {
                    _controller.OnWallContactExit(_trackedBase);
                    _trackedBase = null;
                }
                else if (_trackedLegacy != null)
                {
#pragma warning disable CS0618
                    _controller.OnWallContactExitLegacy(_trackedLegacy);
#pragma warning restore CS0618
                    _trackedLegacy = null;
                }
            }
        }
    }
}
