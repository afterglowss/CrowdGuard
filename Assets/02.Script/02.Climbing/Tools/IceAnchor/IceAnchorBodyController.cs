using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using CrowdGuard.Environment;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// 앵커 몸통(Body) 전용 Controller.
    /// 역할: 운반(파우치 ↔ 손), 벽 삽입, 벽에서 분리.
    /// 회전 로직은 담당하지 않음 (→ IceAnchorHandleController).
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class IceAnchorBodyController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAnchorModel _model;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
        private Rigidbody _rb;

        [Header("Lock Settings")]
        [Tooltip("이 진행도 이상이면 Grip으로 벽에서 떼낼 수 없음 (0~1)")]
        [SerializeField][Range(0f, 1f)] private float _lockThreshold = 0.3f;

        // --- 벽 접촉 상태 ---
        private bool _isTriggerHeld = false;
        private bool _isTouchingWall = false;
        private ClimbableSurface _currentSurface = null;
        private Vector3 _wallContactPoint;
        private Vector3 _wallNormal;

        private void Awake()
        {
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            // Rigidbody는 Body 자신에 있음
            _rb = GetComponent<Rigidbody>();

            if (_model == null)
                _model = GetComponentInParent<IceAnchorModel>();
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

        // ===================== XRI 이벤트 =====================

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_model.IsInserted)
            {
                if (_model.ScrewProgress >= _lockThreshold)
                {
                    // --- 잠금 상태 → 떼낼 수 없음 (잡아도 아무 일 없음) ---
                    Debug.Log("[BodyController] 잠금 상태 — 벽에서 떼낼 수 없습니다. 먼저 손잡이로 해체하세요.");
                    _grabInteractable.trackPosition = false;
                    _grabInteractable.trackRotation = false;
                    return;
                }

                // --- 미잠금 → 벽에서 떼내서 손에 들기 ---
                Debug.Log($"[BodyController] 미잠금 앵커를 벽에서 떼냅니다. (progress={_model.ScrewProgress:F2})");
                DetachFromWall();
                _grabInteractable.trackPosition = true;
                _grabInteractable.trackRotation = true;
            }
            else
            {
                // --- 일반 잡기 (파우치에서 꺼냄) ---
                Debug.Log("[BodyController] 앵커를 손에 쥐었습니다.");
                _grabInteractable.trackPosition = true;
                _grabInteractable.trackRotation = true;
            }

            _model.IsHeld = true;
        }

        private void OnDropped(SelectExitEventArgs args)
        {
            if (_model.IsInserted)
            {
                // 벽에 박힌 상태에서 손 뗌 → 벽에 유지
                Debug.Log("[BodyController] 앵커에서 손을 뗐습니다. 벽에 유지.");
            }
            else
            {
                // 일반 놓기 → 중력 낙하
                Debug.Log("[BodyController] 앵커를 허공에서 놓았습니다.");
                _rb.constraints = RigidbodyConstraints.None;
                _rb.useGravity = true;
                _rb.isKinematic = false;
            }

            _model.IsHeld = false;
            _grabInteractable.trackPosition = true;
            _grabInteractable.trackRotation = true;
        }

        private void OnTriggerActivated(ActivateEventArgs args)
        {
            _isTriggerHeld = true;

            if (!_model.IsInserted && _isTouchingWall)
            {
                TryInsertIntoWall();
            }
        }

        private void OnTriggerDeactivated(DeactivateEventArgs args)
        {
            _isTriggerHeld = false;
        }

        // ===================== 벽 접촉 (from IceAnchorTip) =====================

        public void OnWallContactEnter(ClimbableSurface surface, Vector3 contactPoint, Vector3 normal)
        {
            if (surface.Type == SurfaceType.Rock)
            {
                Debug.Log("[BodyController] 바위에는 앵커를 설치할 수 없습니다.");
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

        public void OnWallContactExit(ClimbableSurface surface)
        {
            if (_currentSurface == surface)
            {
                _isTouchingWall = false;
                _currentSurface = null;
                _model.IsContactingWall = false;
            }
        }

        // ===================== 삽입 / 분리 =====================

        private void TryInsertIntoWall()
        {
            if (!_isTriggerHeld) return;
            if (!_model.IsHeld) return;
            if (!_isTouchingWall) return;
            if (_currentSurface == null) return;
            if (_model.IsInserted) return;

            Debug.Log("[BodyController] 벽면에 앵커를 삽입합니다.");

            // 루트 오브젝트를 벽 법선 방향으로 정렬
            Transform root = _model.transform;
            Quaternion targetRotation = Quaternion.LookRotation(-_wallNormal, Vector3.up);
            root.SetPositionAndRotation(_wallContactPoint, targetRotation);

            // 물리 고정
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.constraints = RigidbodyConstraints.FreezeAll;

            // 위치 추적 비활성화 (벽에 고정)
            _grabInteractable.trackPosition = false;
            _grabInteractable.trackRotation = false;

            _model.IsInserted = true;
        }

        private void DetachFromWall()
        {
            _model.IsInserted = false;
            _model.ScrewProgress = 0f;
            _model.IsFullySecured = false;

            _rb.constraints = RigidbodyConstraints.None;
            // useGravity는 View의 HandleHeldState(true)에서 false로 설정됨

            Debug.Log("[BodyController] 앵커가 벽에서 분리되었습니다.");
        }
    }
}
