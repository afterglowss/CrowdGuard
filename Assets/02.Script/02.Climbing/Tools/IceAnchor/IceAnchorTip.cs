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

            ClimbableSurface surface = other.GetComponent<ClimbableSurface>();
            if (surface == null) return;

            Vector3 contactPoint = other.ClosestPoint(transform.position);
            Vector3 normal = (transform.position - contactPoint).normalized;

            _controller.OnWallContactEnter(surface, contactPoint, normal);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_controller == null) return;

            ClimbableSurface surface = other.GetComponent<ClimbableSurface>();
            if (surface == null) return;

            _controller.OnWallContactExit(surface);
        }
    }
}
