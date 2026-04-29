using UnityEngine;
using CrowdGuard.Environment;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// 아이스 앵커의 끝(삽입 부분) 전용 센서 스크립트.
    /// 물리 엔진의 충돌(Trigger Collider)을 감지하고 Controller로 릴레이합니다.
    /// </summary>
    public class IceAnchorTip : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("루트의 IceAnchorController를 연결해 주세요.")]
        [SerializeField] private IceAnchorController _controller;

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null) return;

            // BaseSurface 우선, ClimbableSurface 폴백
            BaseSurface baseSurface = other.GetComponentInParent<BaseSurface>();
            if (baseSurface != null)
            {
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                Vector3 normal = (transform.position - contactPoint).normalized;
                _controller.OnWallContactEnter(baseSurface, contactPoint, normal);
                return;
            }

#pragma warning disable CS0618 // Obsolete 경고 무시 (테스트용 폴백)
            ClimbableSurface legacySurface = other.GetComponentInParent<ClimbableSurface>();
#pragma warning restore CS0618
            if (legacySurface != null)
            {
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                Vector3 normal = (transform.position - contactPoint).normalized;
                _controller.OnWallContactEnterLegacy(legacySurface, contactPoint, normal);
                return;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_controller == null) return;

            BaseSurface baseSurface = other.GetComponentInParent<BaseSurface>();
            if (baseSurface != null)
            {
                _controller.OnWallContactExit(baseSurface);
                return;
            }

#pragma warning disable CS0618
            ClimbableSurface legacySurface = other.GetComponentInParent<ClimbableSurface>();
#pragma warning restore CS0618
            if (legacySurface != null)
                _controller.OnWallContactExitLegacy(legacySurface);
        }
    }
}
