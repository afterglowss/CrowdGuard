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

        /// <summary>
        /// 앵커 완전 체결 시 발생하는 전역 이벤트 (세이브포인트 등에서 구독)
        /// </summary>
        public static event System.Action<Vector3> OnAnchorSecuredGlobal;

        /// <summary>
        /// 외부 스크립트(HandleController 등)에서 체결 이벤트를 발화할 수 있도록 제공하는 릴레이 메서드
        /// </summary>
        public static void RaiseAnchorSecured(Vector3 position)
        {
            OnAnchorSecuredGlobal?.Invoke(position);
        }

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
            // 영구 고정: 한 번 완전 체결되면 손잡이가 더 이상 돌아가지 않음
            if (_model.IsFullySecured) return;

            float currentAngle = GetControllerAngleAroundInsertionAxis();
            float deltaAngle = Mathf.DeltaAngle(_previousAngle, currentAngle);

            // 태엽 감기: 역방향 회전이나 손목 떨림에 의해 진행도가 깎이지 않도록 절대값을 누적하여 100% 도달 보장
            _accumulatedAngle += Mathf.Abs(deltaAngle);

            float totalRequired = _requiredTurns * 360f;
            float newProgress = Mathf.Clamp01(_accumulatedAngle / totalRequired);
            _model.ScrewProgress = newProgress;

            // 완전 체결 판정
            if (newProgress >= 1.0f && !_model.IsFullySecured)
            {
                _model.IsFullySecured = true;
                Debug.Log("[IceAnchorController] ===== 앵커 완전 체결! (영구 고정) =====");
                OnAnchorSecuredGlobal?.Invoke(transform.position);
            }

            _previousAngle = currentAngle;
        }

        /// <summary>
        /// 태엽 감기처럼 X축(오른쪽) 기준으로 컨트롤러 각도를 측정합니다.
        /// </summary>
        private float GetControllerAngleAroundInsertionAxis()
        {
            Vector3 rotationAxis = transform.right; // Z에서 X축(Right)으로 변경
            
            // 태엽을 감듯 손목을 위아래로 까딱이는 모션(Pitch)은 컨트롤러의 Forward 벡터 변화로 감지하는 것이 가장 직관적
            Vector3 controllerForward = _interactorTransform.forward;
            Vector3 projected = Vector3.ProjectOnPlane(controllerForward, rotationAxis).normalized;
            
            // 기준축은 모델의 Forward 방향
            Vector3 referenceForward = Vector3.ProjectOnPlane(transform.forward, rotationAxis).normalized;
            
            float angle = Vector3.SignedAngle(referenceForward, projected, rotationAxis);
            return angle;
        }
    }
}
