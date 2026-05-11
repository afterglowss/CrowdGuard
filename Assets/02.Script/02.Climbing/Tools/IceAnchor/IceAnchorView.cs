using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using CrowdGuard.XR;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// Model 이벤트를 구독하여 시각·물리·햅틱 피드백을 처리하는 View.
    /// Rigidbody 조작은 이 클래스에서만 담당합니다.
    /// </summary>
    [RequireComponent(typeof(IceAnchorModel))]
    public class IceAnchorView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAnchorModel _model;
        [Tooltip("루트 오브젝트의 Rigidbody")]
        [SerializeField] private Rigidbody _rb;

        [Header("Haptics")]
        [SerializeField] private CrowdGuard.XR.Haptics.HapticProfile _onInsertHaptic;
        [SerializeField] private CrowdGuard.XR.Haptics.HapticProfile _onSecuredHaptic;

        [Tooltip("삽입 햅틱 — Body의 XRGrabInteractable")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _bodyGrabInteractable;

        [Tooltip("체결 햅틱 — Handle의 XRSimpleInteractable")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _handleSimpleInteractable;

        [Header("Handle Visual (손잡이 회전 피드백)")]
        [Tooltip("회전할 손잡이의 Transform (콜라이더+메시 포함)")]
        [SerializeField] private Transform _handleVisual;
        [Tooltip("체결 완료 시 손잡이 총 회전 각도 (도)")]
        [SerializeField] private float _totalHandleAngle = 360f;

        [Header("삽입 깊이 (체결 진행도 연동)")]
        [Tooltip("체결 완료(progress=1) 시 로컬 Z축으로 이동할 최대 거리 (m)")]
        [SerializeField] private float _maxPenetrationDepth = 0.05f;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAnchorModel>();
            if (_rb == null) _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (_model != null)
            {
                _model.OnHeldStateChanged += HandleHeldState;
                _model.OnInsertedStateChanged += HandleInsertedState;
                _model.OnScrewProgressChanged += HandleScrewProgress;
                _model.OnFullySecuredChanged += HandleFullySecuredChanged;
            }
        }

        private void OnDisable()
        {
            if (_model != null)
            {
                _model.OnHeldStateChanged -= HandleHeldState;
                _model.OnInsertedStateChanged -= HandleInsertedState;
                _model.OnScrewProgressChanged -= HandleScrewProgress;
                _model.OnFullySecuredChanged -= HandleFullySecuredChanged;
            }
        }

        // ===================== 물리 피드백 (Rigidbody 단독 관할) =====================

        private void HandleHeldState(bool isHeld)
        {
            if (_rb == null) return;
            if (isHeld)
            {
                _rb.useGravity = false;
                _rb.isKinematic = false;
            }
            else if (!_model.IsInserted)
            {
                // 잡고 있지 않고, 벽에도 안 박혀 있으면 → 낙하
                _rb.useGravity = true;
                _rb.isKinematic = false;
                _rb.constraints = RigidbodyConstraints.None;
            }
        }

        private void HandleInsertedState(bool isInserted)
        {
            if (_rb == null) return;
            if (isInserted)
            {
                // 벽에 박힘 → 물리 완전 고정
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                SendHapticVia(_bodyGrabInteractable, _onInsertHaptic);
            }
            else
            {
                // 벽에서 빠짐 → 물리 해제 + 낙하 + 시각 초기화
                _rb.constraints = RigidbodyConstraints.None;
                _rb.useGravity = true;
                _rb.isKinematic = false;
                if (_handleVisual != null)
                    _handleVisual.localRotation = Quaternion.identity;
                transform.position -= transform.forward * _penetrationOffset;
                _penetrationOffset = 0f;
            }
        }

        // ===================== 시각 피드백 =====================

        private float _penetrationOffset;

        private void HandleScrewProgress(float progress)
        {
            // 핸들 회전
            if (_handleVisual != null)
            {
                float angle = progress * _totalHandleAngle;
                _handleVisual.localRotation = Quaternion.AngleAxis(angle, Vector3.back);
            }

            // 삽입 깊이 — 로컬 forward(Z) 방향으로 이동
            float targetOffset = progress * _maxPenetrationDepth;
            float delta = targetOffset - _penetrationOffset;
            transform.position += transform.forward * delta;
            _penetrationOffset = targetOffset;
        }

        private void HandleFullySecuredChanged(bool isSecured)
        {
            if (isSecured)
            {
                Debug.Log("[AnchorView] ===== 앵커 완전 체결 =====");
                SendHapticVia(_handleSimpleInteractable, _onSecuredHaptic);
            }
        }

        // ===================== Haptics =====================

        /// <summary>
        /// 지정한 Interactable을 현재 잡고 있는 컨트롤러에 햅틱을 전송합니다.
        /// </summary>
        private void SendHapticVia(
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable,
            CrowdGuard.XR.Haptics.HapticProfile hapticProfile)
        {
            if (interactable == null || hapticProfile == null) return;

            var interactors = interactable.interactorsSelecting;
            if (interactors.Count == 0) return;

            var provider = (interactors[0] as MonoBehaviour)?.GetComponentInParent<CrowdGuard.XR.Haptics.IHapticProvider>();
            provider?.PlayHaptic(hapticProfile);
        }
    }
}
