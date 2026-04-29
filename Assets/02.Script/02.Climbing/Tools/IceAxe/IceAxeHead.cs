using UnityEngine;
using CrowdGuard.Environment; // 지형(Surface) 클래스를 불러옵니다.

namespace CrowdGuard.Climbing.Tools.IceAxe
{
    /// <summary>
    /// 아이스 바일의 머리(찍는 부분) 전용 센서 스크립트.
    /// 오로지 물리 엔진의 충돌(Collider)을 감지하고 IceAxeController로 릴레이해줍니다.
    /// 독립된 자식 오브젝트(Sphere Collider)에 단독으로 배치됩니다.
    /// </summary>
    public class IceAxeHead : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("아이스 바일 몸체에 달린 컨트롤러를 연결해 주세요.")]
        [SerializeField] private IceAxeController _controller;

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null) return;
            BaseSurface surface = other.GetComponentInParent<BaseSurface>();
            if (surface == null) return;

            _controller.OnIceContactEnter(surface);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_controller == null) return;

            BaseSurface surface = other.GetComponentInParent<BaseSurface>();
            if (surface == null) return;

            _controller.OnIceContactExit(surface);
        }
    }
}
