using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// 앵커 손잡이(Handle) 전용 Controller.
    /// XRSimpleInteractable을 사용하여 Rigidbody 없이 select 이벤트만 수신합니다.
    /// Grip으로 손잡이를 잡으면 회전 추적이 시작되며, Trigger는 불필요합니다.
    /// 앵커가 벽에 삽입되었을 때만 활성화됩니다.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class IceAnchorHandleController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAnchorModel _model;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _simpleInteractable;

        [Header("Screw Settings (회전 체결)")]
        [Tooltip("체결에 필요한 바퀴 수. 1.0 = 360° 한 바퀴")]
        [SerializeField] private float _requiredTurns = 1.0f;

        // --- 회전 추적 ---
        private Transform _interactorTransform;
        private float _previousAngle;
        private float _accumulatedAngle = 0f;
        private bool _isTracking = false;

        private void Awake()
        {
            _simpleInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

            if (_model == null)
                _model = GetComponentInParent<IceAnchorModel>();

            // 시작 시 비활성화 (삽입 전에는 잡기 불가)
            _simpleInteractable.enabled = false;
        }

        private void OnEnable()
        {
            if (_model != null)
            {
                _model.OnInsertedStateChanged += HandleInsertedState;
            }
            if (_simpleInteractable != null)
            {
                _simpleInteractable.selectEntered.AddListener(OnGrabbed);
                _simpleInteractable.selectExited.AddListener(OnDropped);
            }
        }

        private void OnDisable()
        {
            if (_model != null)
            {
                _model.OnInsertedStateChanged -= HandleInsertedState;
            }
            if (_simpleInteractable != null)
            {
                _simpleInteractable.selectEntered.RemoveListener(OnGrabbed);
                _simpleInteractable.selectExited.RemoveListener(OnDropped);
            }
        }

        private void Update()
        {
            if (!_isTracking) return;
            if (_interactorTransform == null) return;

            TrackScrewRotation();
        }

        // ===================== Model 이벤트 =====================

        private void HandleInsertedState(bool isInserted)
        {
            _simpleInteractable.enabled = isInserted;

            if (!isInserted)
            {
                _isTracking = false;
                _accumulatedAngle = 0f;
                _interactorTransform = null;
            }
        }

        // ===================== XRI 이벤트 =====================

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Debug.Log("[HandleController] 손잡이를 잡았습니다. 회전 추적 시작.");

            _interactorTransform = args.interactorObject.transform;
            _accumulatedAngle = _model.ScrewProgress * _requiredTurns * 360f;
            _previousAngle = GetControllerAngleAroundInsertionAxis();
            _isTracking = true;
        }

        private void OnDropped(SelectExitEventArgs args)
        {
            Debug.Log("[HandleController] 손잡이에서 손을 뗐습니다.");

            _isTracking = false;
            _interactorTransform = null;
        }

        // ===================== 태엽식 회전 (X축, 절대값 누적) =====================

        private void TrackScrewRotation()
        {
            // 영구 고정: 한 번 완전 체결되면 손잡이가 더 이상 돌아가지 않음
            if (_model.IsFullySecured) return;

            float currentAngle = GetControllerAngleAroundInsertionAxis();
            float deltaAngle = Mathf.DeltaAngle(_previousAngle, currentAngle);

            // 태엽 감기: 어느 방향으로 돌려도 진행도가 오르기만 함 (절대값 누적)
            _accumulatedAngle += Mathf.Abs(deltaAngle);

            float totalRequired = _requiredTurns * 360f;
            float newProgress = Mathf.Clamp01(_accumulatedAngle / totalRequired);
            _model.ScrewProgress = newProgress;

            // 완전 체결 판정 (영구 고정 + 세이브포인트 전역 이벤트 발화)
            if (newProgress >= 1.0f)
            {
                _model.IsFullySecured = true;
                Debug.Log("[HandleController] ===== 앵커 완전 체결! (영구 고정) =====");
                IceAnchorController.RaiseAnchorSecured(_model.transform.position);
            }

            _previousAngle = currentAngle;
        }

        /// <summary>
        /// X축(Right) 기준으로 컨트롤러 각도를 측정합니다 (태엽 감기 모션).
        /// </summary>
        private float GetControllerAngleAroundInsertionAxis()
        {
            Transform root = _model.transform;
            Vector3 rotationAxis = root.right; // X축 태엽 감기

            Vector3 controllerForward = _interactorTransform.forward;
            Vector3 projected = Vector3.ProjectOnPlane(controllerForward, rotationAxis).normalized;

            Vector3 referenceForward = Vector3.ProjectOnPlane(root.forward, rotationAxis).normalized;
            return Vector3.SignedAngle(referenceForward, projected, rotationAxis);
        }
    }
}
