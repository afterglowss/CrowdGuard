using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CrowdGuard.XR;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// Model 이벤트를 구독하여 시각·물리·햅틱 피드백을 처리하는 View.
    /// Rigidbody는 Body 자식 오브젝트에 있으므로 SerializeField로 참조합니다.
    /// </summary>
    [RequireComponent(typeof(IceAnchorModel))]
    public class IceAnchorView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAnchorModel _model;
        [Tooltip("Body 오브젝트의 Rigidbody를 연결")]
        [SerializeField] private Rigidbody _rb;

        [Header("Haptics")]
        [Tooltip("앵커 삽입 시 재생할 햅틱 프로파일")]
        [SerializeField] private CrowdGuard.XR.Haptics.HapticProfile _onInsertHaptic;

        [Tooltip("앵커 체결 완료 시 재생할 햅틱 프로파일")]
        [SerializeField] private CrowdGuard.XR.Haptics.HapticProfile _onSecuredHaptic;

        [Tooltip("삽입 햅틱 — Body의 XRGrabInteractable")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _bodyGrabInteractable;

        [Tooltip("체결 햅틱 — Handle의 XRSimpleInteractable")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _handleSimpleInteractable;

        [Header("Handle Visual (손잡이 회전 피드백)")]
        [Tooltip("회전할 손잡이의 Transform")]
        [SerializeField] private Transform _handleVisual;
        [Tooltip("체결 완료 시 손잡이 총 회전 각도 (도)")]
        [SerializeField] private float _totalHandleAngle = 360f;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAnchorModel>();
            if (_rb == null) _rb = GetComponentInChildren<Rigidbody>();
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

        // ===================== State Handlers =====================

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
                _rb.useGravity = true;
                _rb.isKinematic = false;
            }
        }

        private void HandleInsertedState(bool isInserted)
        {
            if (_rb == null) return;
            if (isInserted)
            {
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                SendHapticVia(_bodyGrabInteractable, _onInsertHaptic);
            }
            else
            {
                _rb.constraints = RigidbodyConstraints.None;
                if (_handleVisual != null)
                    _handleVisual.localRotation = Quaternion.identity;
            }
        }

        private void HandleScrewProgress(float progress)
        {
            if (_handleVisual != null)
            {
                float angle = progress * _totalHandleAngle;
                _handleVisual.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }

        private void HandleFullySecuredChanged(bool isSecured)
        {
            if (isSecured)
            {
                Debug.Log("[IceAnchorView] ===== 앵커 완전 체결 =====");
                SendHapticVia(_handleSimpleInteractable, _onSecuredHaptic);
            }
            else
            {
                Debug.Log("[IceAnchorView] 앵커 체결 해제됨.");
            }
        }

        // ===================== Haptics =====================

        /// <summary>
        /// 지정한 Interactable을 현재 잡고 있는 컨트롤러에 햅틱을 전송합니다.
        /// XRGrabInteractable과 XRSimpleInteractable 모두 지원 (XRBaseInteractable 베이스).
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
