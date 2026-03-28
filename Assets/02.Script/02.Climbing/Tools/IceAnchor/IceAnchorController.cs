using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using CrowdGuard.Environment;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// XRI 이벤트 수신 + 사용자 입력 → Model 상태 변경.
    /// 
    /// ■ 동작 흐름:
    /// 1) Grip으로 앵커를 듦 → 벽에 밀착 + Trigger → 벽에 삽입 (Grip/Trigger 놓아도 벽에 유지)
    /// 2) 삽입 상태에서 Grip → 손잡이 잡기 (앵커는 벽에 고정)
    ///    - Trigger 누르면 → 게이트밸브처럼 회전 (양방향)
    ///    - Grip 놓으면 → screwProgress < lockThreshold이면 벽에서 떼냄, 아니면 유지
    /// 3) 완전 체결(screwProgress=1) → 앵커 기능(세이브 포인트) 활성화 + 햅틱
    /// 4) 역회전으로 해체 가능 → 잠금 해제 후 Grip 놓으면 회수
    /// </summary>
    [RequireComponent(typeof(IceAnchorModel), typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class IceAnchorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAnchorModel _model;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
        private Rigidbody _rb;

        [Header("Screw Settings (회전 체결)")]
        [Tooltip("체결에 필요한 바퀴 수. 1.0 = 360° 한 바퀴")]
        [SerializeField] private float _requiredTurns = 1.0f;

        [Tooltip("이 진행도 이상이면 Grip으로 벽에서 떼낼 수 없음 (0~1)")]
        [SerializeField][Range(0f, 1f)] private float _lockThreshold = 0.3f;

        // --- 내부 상태 ---
        private bool _isTriggerHeld = false;
        private bool _isTouchingWall = false;
        private ClimbableSurface _currentSurface = null;
        private Vector3 _wallContactPoint;
        private Vector3 _wallNormal;

        // 회전 추적용
        private Transform _interactorTransform;
        private float _previousAngle;
        private float _accumulatedAngle = 0f;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAnchorModel>();
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.AddListener(OnGrabbed);
                _grabInteractable.selectExited.AddListener(OnDropped);
                _grabInteractable.activated.AddListener(OnTriggerActivated);
                _grabInteractable.deactivated.AddListener(OnTriggerDeactivated);
            }
        }

        private void OnDisable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                _grabInteractable.selectExited.RemoveListener(OnDropped);
                _grabInteractable.activated.RemoveListener(OnTriggerActivated);
                _grabInteractable.deactivated.RemoveListener(OnTriggerDeactivated);
            }
        }

        private void Update()
        {
            // 삽입 상태 + Trigger 유지 + 컨트롤러 있음 → 회전 추적
            if (_model == null) return;
            if (!_model.IsInserted) return;
            if (!_isTriggerHeld) return;
            if (_interactorTransform == null) return;

            TrackScrewRotation();
        }

        // ===================== XRI 이벤트 핸들러 =====================

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _interactorTransform = args.interactorObject.transform;

            if (_model.IsInserted)
            {
                if (_model.ScrewProgress < _lockThreshold)
                {
                    // --- 미잠금 → 벽에서 떼내서 손에 들기 ---
                    Debug.Log($"[IceAnchorController] 미잠금 앵커를 벽에서 떼냅니다. (progress={_model.ScrewProgress:F2} < threshold={_lockThreshold})");
                    DetachFromWall();

                    _grabInteractable.trackPosition = true;
                    _grabInteractable.trackRotation = true;
                }
                else
                {
                    // --- 잠금 상태 → 손잡이만 잡기 (앵커는 벽에 유지) ---
                    Debug.Log("[IceAnchorController] 잠금 앵커의 손잡이를 잡았습니다.");
                    _grabInteractable.trackPosition = false;
                    _grabInteractable.trackRotation = false;
                }
            }
            else
            {
                // --- 일반 잡기 (파우치에서 꺼낸 앵커) ---
                Debug.Log("[IceAnchorController] 앵커를 손에 쥐었습니다.");
                _grabInteractable.trackPosition = true;
                _grabInteractable.trackRotation = true;
            }

            _model.IsHeld = true;
        }

        private void OnDropped(SelectExitEventArgs args)
        {
            _interactorTransform = null;

            if (_model.IsInserted)
            {
                // --- 벽에 박힌 앵커에서 손을 뗌 → 벽에 그대로 유지 ---
                Debug.Log("[IceAnchorController] 앵커 손잡이에서 손을 뗐습니다. 벽에 유지.");
            }
            else
            {
                // --- 일반 놓기 (공중에서 놓음) ---
                Debug.Log("[IceAnchorController] 앵커를 허공에서 놓았습니다.");
                _rb.constraints = RigidbodyConstraints.None;
                _rb.useGravity = true;
                _rb.isKinematic = false;
            }

            _model.IsHeld = false;

            // 위치/회전 추적을 기본값으로 복원 (다음 잡기를 위해)
            _grabInteractable.trackPosition = true;
            _grabInteractable.trackRotation = true;
        }

        private void OnTriggerActivated(ActivateEventArgs args)
        {
            _isTriggerHeld = true;

            if (!_model.IsInserted && _isTouchingWall)
            {
                // 아직 삽입 안 됨 + 벽에 닿아 있음 → 삽입 시도
                Debug.Log("[IceAnchorController] Trigger — 삽입 시도.");
                TryInsertIntoWall();
            }
            else if (_model.IsInserted)
            {
                // 이미 삽입됨 → 회전 모드 진입, 현재 각도 기록
                Debug.Log("[IceAnchorController] Trigger — 회전 모드 진입.");
                if (_interactorTransform != null)
                {
                    _previousAngle = GetControllerAngleAroundInsertionAxis();
                }
            }
        }

        private void OnTriggerDeactivated(DeactivateEventArgs args)
        {
            Debug.Log("[IceAnchorController] Trigger Released.");
            _isTriggerHeld = false;
        }

        // ===================== 벽 접촉 콜백 (from IceAnchorTip) =====================

        public void OnWallContactEnter(ClimbableSurface surface, Vector3 contactPoint, Vector3 normal)
        {
            if (surface.Type == SurfaceType.Rock)
            {
                Debug.Log("[IceAnchorController] 바위에는 앵커를 설치할 수 없습니다.");
                return;
            }

            _isTouchingWall = true;
            _currentSurface = surface;
            _wallContactPoint = contactPoint;
            _wallNormal = normal;

            if (_model != null) _model.IsContactingWall = true;

            if (_isTriggerHeld && !_model.IsInserted)
            {
                TryInsertIntoWall();
            }
        }

        public void OnWallContactExit(ClimbableSurface surface)
        {
            if (_currentSurface == surface)
            {
                _isTouchingWall = false;
                _currentSurface = null;
                if (_model != null) _model.IsContactingWall = false;
            }
        }

        // ===================== 삽입 로직 =====================

        private void TryInsertIntoWall()
        {
            if (!_isTriggerHeld) return;
            if (_model == null) return;
            if (!_model.IsHeld) return;
            if (!_isTouchingWall) return;
            if (_currentSurface == null) return;
            if (_model.IsInserted) return;

            Debug.Log("[IceAnchorController] 삽입 조건 만족! 벽면에 앵커를 삽입합니다.");

            // 벽 법선 방향으로 자동 정렬
            AlignToWallNormal();

            // 물리 고정
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.constraints = RigidbodyConstraints.FreezeAll;

            // 위치 추적 비활성화 (벽에 고정되어야 하므로)
            _grabInteractable.trackPosition = false;
            _grabInteractable.trackRotation = false;

            // 회전 추적 초기화
            _accumulatedAngle = 0f;
            _model.ScrewProgress = 0f;
            if (_interactorTransform != null)
            {
                _previousAngle = GetControllerAngleAroundInsertionAxis();
            }

            _model.IsInserted = true;
        }

        /// <summary>
        /// 앵커를 벽에서 분리합니다. (잠금 미달 시 호출)
        /// </summary>
        private void DetachFromWall()
        {
            _model.IsInserted = false;
            _model.ScrewProgress = 0f;
            _model.IsFullySecured = false;
            _accumulatedAngle = 0f;

            _rb.constraints = RigidbodyConstraints.None;
            _rb.useGravity = true;
            _rb.isKinematic = false;

            Debug.Log("[IceAnchorController] 앵커가 벽에서 분리되었습니다.");
        }

        private void AlignToWallNormal()
        {
            Quaternion targetRotation = Quaternion.LookRotation(-_wallNormal, Vector3.up);
            transform.SetPositionAndRotation(_wallContactPoint, targetRotation);
        }

        // ===================== 게이트밸브 회전 (양방향) =====================

        private void TrackScrewRotation()
        {
            float currentAngle = GetControllerAngleAroundInsertionAxis();
            float deltaAngle = Mathf.DeltaAngle(_previousAngle, currentAngle);

            // 양방향: 정방향(+) = 체결, 역방향(−) = 해체
            _accumulatedAngle += deltaAngle;

            // 0 미만 클램핑 (완전 분리 이하는 없음)
            _accumulatedAngle = Mathf.Max(0f, _accumulatedAngle);

            float totalRequired = _requiredTurns * 360f;
            float newProgress = Mathf.Clamp01(_accumulatedAngle / totalRequired);
            _model.ScrewProgress = newProgress;

            // 완전 체결 / 해체 판정
            if (newProgress >= 1.0f && !_model.IsFullySecured)
            {
                _model.IsFullySecured = true;
                Debug.Log("[IceAnchorController] ===== 앵커 완전 체결! =====");
            }
            else if (newProgress < 1.0f && _model.IsFullySecured)
            {
                _model.IsFullySecured = false;
                Debug.Log("[IceAnchorController] 앵커 체결 해제됨.");
            }

            _previousAngle = currentAngle;
        }

        /// <summary>
        /// 삽입축(앵커의 forward = 벽 안쪽 방향) 기준으로 컨트롤러의 현재 각도를 계산합니다.
        /// </summary>
        private float GetControllerAngleAroundInsertionAxis()
        {
            Vector3 insertionAxis = transform.forward;
            Vector3 controllerUp = _interactorTransform.up;
            Vector3 projected = Vector3.ProjectOnPlane(controllerUp, insertionAxis).normalized;
            Vector3 referenceUp = Vector3.ProjectOnPlane(transform.up, insertionAxis).normalized;
            float angle = Vector3.SignedAngle(referenceUp, projected, insertionAxis);
            return angle;
        }
    }
}
