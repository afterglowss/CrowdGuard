using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using CrowdGuard.Environment;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// 아이스 앵커의 통합 Controller.
    /// Body(XRGrabInteractable)와 Handle(XRSimpleInteractable) 양쪽 XRI 이벤트를 수신하여
    /// Model 상태를 변경합니다.
    ///
    /// ■ 동작 흐름:
    /// 1) Body를 Grip으로 잡아서 운반
    /// 2) 벽에 밀착 + Trigger → 삽입 (Body Grip 놓아도 벽에 유지)
    /// 3) 삽입 후 Handle을 Grip으로 잡으면 회전 추적 시작
    /// 4) 손목 회전으로 ScrewProgress 누적 → 100%에서 영구 체결
    /// 5) 체결 시 SavePointManager에 전역 이벤트 방송
    /// </summary>
    public class IceAnchorController : MonoBehaviour
    {
        // ===================== References =====================

        [Header("References")]
        [SerializeField] private IceAnchorModel _model;

        [Tooltip("Body 자식의 XRGrabInteractable")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _bodyGrab;

        [Tooltip("Handle 자식의 XRSimpleInteractable")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _handleSimple;

        [Tooltip("Body 자식의 Rigidbody")]
        [SerializeField] private Rigidbody _rb;

        [Tooltip("회전 시각 피드백용 Handle Transform (콜라이더+메시 포함 루트)")]
        [SerializeField] private Transform _handleVisual;

        // ===================== Settings =====================

        [Header("Screw Settings")]
        [Tooltip("체결에 필요한 바퀴 수. 1.0 = 360°")]
        [SerializeField] private float _requiredTurns = 1.0f;

        [Tooltip("역회전으로 이 진행도 이하가 되면 자동 분리 (-0.1 = -10%)")]
        [SerializeField] private float _detachThreshold = -0.1f;

        // ===================== Global Event =====================

        /// <summary>
        /// 앵커 완전 체결 시 발생하는 전역 이벤트. SavePointManager 등에서 구독.
        /// </summary>
        public static event Action<IceAnchorModel> OnAnchorSecuredGlobal;

        // ===================== Internal State =====================

        // Body 관련
        private bool _isTriggerHeld;
        private bool _isTouchingWall;
        private BaseSurface _currentSurface;
        private Vector3 _wallContactPoint;
        private Vector3 _wallNormal;

        // Handle 회전 관련
        private Transform _handleInteractorTransform;
        private float _previousAngle;
        private float _accumulatedAngle;
        private bool _isHandleGrabbed;

        // ===================== Lifecycle =====================

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAnchorModel>();
        }

        private void OnEnable()
        {
            // Body 이벤트
            if (_bodyGrab != null)
            {
                _bodyGrab.selectEntered.AddListener(OnBodyGrabbed);
                _bodyGrab.selectExited.AddListener(OnBodyDropped);
                _bodyGrab.activated.AddListener(OnBodyTriggerActivated);
                _bodyGrab.deactivated.AddListener(OnBodyTriggerDeactivated);
            }

            // Handle 이벤트
            if (_handleSimple != null)
            {
                _handleSimple.selectEntered.AddListener(OnHandleGrabbed);
                _handleSimple.selectExited.AddListener(OnHandleDropped);
                // 삽입 전에는 핸들 잡기 비활성화
                _handleSimple.enabled = false;
            }

            // Model 이벤트 (삽입 상태 변경 시 핸들 활성화/비활성화)
            if (_model != null)
            {
                _model.OnInsertedStateChanged += HandleInsertedStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_bodyGrab != null)
            {
                _bodyGrab.selectEntered.RemoveListener(OnBodyGrabbed);
                _bodyGrab.selectExited.RemoveListener(OnBodyDropped);
                _bodyGrab.activated.RemoveListener(OnBodyTriggerActivated);
                _bodyGrab.deactivated.RemoveListener(OnBodyTriggerDeactivated);
            }

            if (_handleSimple != null)
            {
                _handleSimple.selectEntered.RemoveListener(OnHandleGrabbed);
                _handleSimple.selectExited.RemoveListener(OnHandleDropped);
            }

            if (_model != null)
            {
                _model.OnInsertedStateChanged -= HandleInsertedStateChanged;
            }
        }

        private void Update()
        {
            if (!_isHandleGrabbed) return;
            if (_handleInteractorTransform == null) return;

            TrackScrewRotation();
        }

        // ===================== Body XRI 이벤트 =====================

        private void OnBodyGrabbed(SelectEnterEventArgs args)
        {
            // 삽입 상태에서는 Body 잡기가 비활성화되므로 여기 오면 항상 비삽입 상태
            Debug.Log("[Anchor] 앵커를 손에 쥐었습니다.");
            _bodyGrab.trackPosition = true;
            _bodyGrab.trackRotation = true;
            _model.IsHeld = true;
        }

        private void OnBodyDropped(SelectExitEventArgs args)
        {
            if (!_model.IsInserted)
            {
                // 허공에서 놓기 → 낙하 (물리 상태는 View가 처리)
                Debug.Log("[Anchor] 앵커를 허공에서 놓았습니다.");
            }
            else
            {
                Debug.Log("[Anchor] 앵커에서 손을 뗐습니다. 벽에 유지.");
            }

            _model.IsHeld = false;
            _bodyGrab.trackPosition = true;
            _bodyGrab.trackRotation = true;
        }

        private void OnBodyTriggerActivated(ActivateEventArgs args)
        {
            _isTriggerHeld = true;

            if (!_model.IsInserted && _isTouchingWall)
            {
                TryInsertIntoWall();
            }
        }

        private void OnBodyTriggerDeactivated(DeactivateEventArgs args)
        {
            _isTriggerHeld = false;
        }

        // ===================== Handle XRI 이벤트 =====================

        private void OnHandleGrabbed(SelectEnterEventArgs args)
        {
            if (_model.IsFullySecured) return;

            Debug.Log("[Anchor] 손잡이를 잡았습니다. 회전 추적 시작.");

            _handleInteractorTransform = args.interactorObject.transform;
            _accumulatedAngle = _model.ScrewProgress * _requiredTurns * 360f;
            _previousAngle = GetControllerAngle();
            _isHandleGrabbed = true;
        }

        private void OnHandleDropped(SelectExitEventArgs args)
        {
            Debug.Log("[Anchor] 손잡이에서 손을 뗐습니다.");

            _isHandleGrabbed = false;
            _handleInteractorTransform = null;
        }

        // ===================== Model 이벤트 =====================

        private void HandleInsertedStateChanged(bool isInserted)
        {
            // 삽입 시: Handle만 잡을 수 있음, Body는 잡기 불가
            if (_handleSimple != null)
                _handleSimple.enabled = isInserted;
            if (_bodyGrab != null)
                _bodyGrab.enabled = !isInserted;

            if (!isInserted)
            {
                _isHandleGrabbed = false;
                _accumulatedAngle = 0f;
                _handleInteractorTransform = null;
            }
        }

        // ===================== 벽 접촉 (from IceAnchorTip) =====================

        public void OnWallContactEnter(BaseSurface surface, Vector3 contactPoint, Vector3 normal)
        {
            if (!surface.CanInstallAnchor())
            {
                Debug.Log("[Anchor] 이 표면에는 앵커를 설치할 수 없습니다.");
                return;
            }

            _isTouchingWall = true;
            _currentSurface = surface;
            _wallContactPoint = contactPoint;
            _wallNormal = normal;
            _model.IsContactingWall = true;

            if (_isTriggerHeld && !_model.IsInserted)
            {
                TryInsertIntoWall();
            }
        }

        public void OnWallContactExit(BaseSurface surface)
        {
            if (_currentSurface == surface)
            {
                _isTouchingWall = false;
                _currentSurface = null;
                _model.IsContactingWall = false;
            }
        }

        // ===================== 벽 접촉 — 레거시 ClimbableSurface 폴백 =====================

#pragma warning disable CS0618
        public void OnWallContactEnterLegacy(ClimbableSurface surface, Vector3 contactPoint, Vector3 normal)
        {
            if (surface.Type == SurfaceType.Rock)
            {
                Debug.Log("[Anchor] 바위에는 앵커를 설치할 수 없습니다. (legacy)");
                return;
            }

            _isTouchingWall = true;
            _currentSurface = null; // 레거시 표면은 BaseSurface가 아님
            _wallContactPoint = contactPoint;
            _wallNormal = normal;
            _model.IsContactingWall = true;

            if (_isTriggerHeld && !_model.IsInserted)
            {
                TryInsertIntoWall();
            }
        }

        public void OnWallContactExitLegacy(ClimbableSurface surface)
        {
            _isTouchingWall = false;
            _currentSurface = null;
            _model.IsContactingWall = false;
        }
#pragma warning restore CS0618

        // ===================== 삽입 / 분리 =====================

        private void TryInsertIntoWall()
        {
            if (!_isTriggerHeld) return;
            if (!_model.IsHeld) return;
            if (!_isTouchingWall) return;
            if (_model.IsInserted) return;

            Debug.Log("[Anchor] 벽면에 앵커를 삽입합니다.");

            // 벽 법선 방향으로 자동 정렬 (Z축 Roll 제거)
            Quaternion targetRotation = Quaternion.LookRotation(-_wallNormal, Vector3.up);
            Vector3 euler = targetRotation.eulerAngles;
            euler.z = 0f;
            targetRotation = Quaternion.Euler(euler);
            transform.SetPositionAndRotation(_wallContactPoint, targetRotation);

            // 위치 추적 비활성화 (벽에 고정)
            _bodyGrab.trackPosition = false;
            _bodyGrab.trackRotation = false;

            _model.IsInserted = true;
        }

        private void DetachFromWall()
        {
            _model.IsInserted = false;
            _model.ScrewProgress = 0f;
            _model.IsFullySecured = false;
            _accumulatedAngle = 0f;

            Debug.Log("[Anchor] 앵커가 벽에서 분리되었습니다.");
        }

        /// <summary>
        /// 핸들을 잡고 있던 손으로 Body 그랩을 이어받아, 앵커가 바닥에 떨어지지 않도록 합니다.
        /// </summary>
        private void DetachWithHandoff()
        {
            // 1) 핸들을 잡고 있던 인터랙터 기억
            UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor = null;
            if (_handleSimple != null && _handleSimple.interactorsSelecting.Count > 0)
                interactor = _handleSimple.interactorsSelecting[0];

            // 2) 벽에서 분리 (Handle 비활성, Body 활성)
            DetachFromWall();

            // 3) 같은 손으로 Body를 즉시 잡기
            if (interactor != null && _bodyGrab != null && _bodyGrab.enabled)
            {
                var manager = _bodyGrab.interactionManager;
                if (manager != null)
                {
                    manager.SelectEnter(interactor, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)_bodyGrab);
                    Debug.Log("[Anchor] 핸들 → 바디로 그랩 전환 완료.");
                }
            }
        }

        // ===================== 회전 체결 =====================

        private void TrackScrewRotation()
        {
            // 영구 고정: 체결 완료 시 회전 불가
            if (_model.IsFullySecured) return;

            float currentAngle = GetControllerAngle();
            float deltaAngle = Mathf.DeltaAngle(_previousAngle, currentAngle);

            // 나사못 — 정방향 조임, 역방향 풀림 (음수 허용)
            _accumulatedAngle += deltaAngle;

            float totalRequired = _requiredTurns * 360f;
            float newProgress = _accumulatedAngle / totalRequired;

            // 역회전으로 detachThreshold 이하 → 핸들→바디 그랩 전환 후 분리
            if (newProgress <= _detachThreshold)
            {
                Debug.Log($"[Anchor] 나사 역회전으로 자동 분리 (progress={newProgress:F2})");
                DetachWithHandoff();
                return;
            }

            _model.ScrewProgress = Mathf.Clamp01(newProgress);

            // 완전 체결
            if (newProgress >= 1.0f)
            {
                _model.IsFullySecured = true;
                Debug.Log("[Anchor] ===== 앵커 완전 체결! (영구 고정) =====");
                OnAnchorSecuredGlobal?.Invoke(_model);
            }

            _previousAngle = currentAngle;
        }

        /// <summary>
        /// -Z축(Back) 기준으로 컨트롤러의 손목 회전(Roll)을 측정합니다.
        /// </summary>
        private float GetControllerAngle()
        {
            Vector3 rotationAxis = -transform.forward;

            Vector3 controllerUp = _handleInteractorTransform.up;
            Vector3 projected = Vector3.ProjectOnPlane(controllerUp, rotationAxis).normalized;

            Vector3 referenceUp = Vector3.ProjectOnPlane(transform.up, rotationAxis).normalized;

            return Vector3.SignedAngle(referenceUp, projected, rotationAxis);
        }
    }
}
