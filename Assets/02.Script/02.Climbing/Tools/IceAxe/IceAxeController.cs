using CrowdGuard.Environment;
using SimpleAudioManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace CrowdGuard.Climbing.Tools.IceAxe
{

    [RequireComponent(typeof(IceAxeModel), typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class IceAxeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAxeModel _model;
        [SerializeField] private IceAxeDepthCorrector _depthCorrector;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;

        [Header("Physics & Rules")]
        [Tooltip("아이스 바일이 벽에 박히기 위해 필요한 최소 속도")]
        [SerializeField] private float _minAttachVelocity = 1.5f;

        private Rigidbody _rb;

        private bool _isTriggerHeld = false;
        private bool _isTouchingIce = false;
        private BaseSurface _currentSurface = null;
        private Vector3 _contactPoint; // 실제 접촉 위치

        // 컨트롤러 속도 직접 추적 (Velocity Damping 영향 없음)
        private Transform _interactorTransform;
        private Vector3 _prevControllerPos;
        private Vector3 _controllerVelocity;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAxeModel>();
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


        private void OnGrabbed(SelectEnterEventArgs args)
        {
            //Debug.Log("[IceAxeController] XRI 그랩 발동 - 플레이어가 손으로 바일을 쥐었습니다!");

            // 파우치와의 부모-자식 관계 해제는 RetractableObject에서 처리하므로 삭제

            if (_model != null) _model.IsHeld = true;

            // 컨트롤러 Transform 캐싱 (속도 추적용 및 FSM 등반 연산용)
            _interactorTransform = args.interactorObject.transform;
            if (_model != null) _model.InteractorTransform = _interactorTransform;

            _prevControllerPos = _interactorTransform.position;
            _controllerVelocity = Vector3.zero;

            if (!_isTriggerHeld && _model != null && _model.IsAttachedToWall)
            {
                //Debug.Log("[IceAxeController] 트리거 없이 바일을 잡았습니다. 벽에서 즉시 뽑아냅니다!");
                // 강제로 벽 부착 상태를 해제하여, FSM이 ClimbingState로 넘어가는 것을 원천 차단합니다.
                _model.IsAttachedToWall = false;
            }
        }

        private void OnDropped(SelectExitEventArgs args)
        {
            //Debug.Log("[IceAxeController] XRI 그랩 해제 - 플레이어가 손에서 바일을 놓았습니다.");
            if (_model != null)
            {
                _model.IsHeld = false;
                _model.InteractorTransform = null;
            }
            _interactorTransform = null;
        }

        private void Update()
        {
            // 박혀있던 벽이 파괴됐으면 바일 자동 분리
            if (_model != null && _model.IsAttachedToWall
                && _currentSurface != null
                && _currentSurface.IsBrokenAt(_contactPoint))
            {
                Debug.Log("[IceAxeController] 부착된 벽이 파괴됨 — 바일 자동 분리");
                _model.IsAttachedToWall = false;
            }

            // 컨트롤러의 실제 이동 속도를 매 프레임 계산
            if (_interactorTransform == null) return;

            _controllerVelocity = (_interactorTransform.position - _prevControllerPos) / Time.deltaTime;
            _prevControllerPos = _interactorTransform.position;
        }


        private void OnTriggerActivated(ActivateEventArgs args)
        {
            //Debug.Log("[IceAxeController] XRI Activate (Trigger) Pressed - 장비 고정 의도 (Trigger 유지 시작)");
            _isTriggerHeld = true;

            if (_model != null)
            {
                _model.InteractorTransform = args.interactorObject.transform;
            }

            TryAttachToWall();
        }

        private void OnTriggerDeactivated(DeactivateEventArgs args)
        {
            //Debug.Log("[IceAxeController] XRI Deactivate (Trigger) Released - 얼음벽에서 바일 분리");
            _isTriggerHeld = false;
            if (_model != null) _model.IsAttachedToWall = false;
        }


        public void OnIceContactEnter(BaseSurface surface, Vector3 contactPoint = default)
        {
            //Debug.Log($"[IceAxeController] 지형 청크에 접근했습니다: {surface.gameObject.name}");
            _isTouchingIce = true;
            _currentSurface = surface;
            _contactPoint = contactPoint;

            // physics step 이후 OnTriggerEnter 타이밍에 호출되므로,
            // 이 시점의 _prevTipCenter는 직전 FixedUpdate에서 기록된 벽 바깥 위치입니다.
            // 이 값을 SphereCast 기점으로 보존합니다.
            _depthCorrector?.OnTipContactBegin();

            TryAttachToWall();
        }

        public void OnIceContactExit(BaseSurface surface)
        {
            if (_currentSurface == surface)
            {
                //Debug.Log("[IceAxeController] 바일 머리가 얼음벽에서 떨어졌습니다.");
                _isTouchingIce = false;
                _currentSurface = null;
                _contactPoint = default;
            }
        }

        /// <summary>
        /// 추락 등 외부 이벤트에서 바일을 강제로 손에서 놓습니다.
        /// XRI SelectExit를 통해 정상 release 흐름(OnDropped)을 타므로
        /// IsHeld, InteractorTransform 등 모든 내부 상태가 깔끔하게 정리됩니다.
        /// </summary>
        public void ForceRelease()
        {
            _isTriggerHeld = false;

            if (_model != null) _model.IsAttachedToWall = false;

            if (_grabInteractable != null && _grabInteractable.isSelected)
            {
                var interactor = _grabInteractable.firstInteractorSelecting;
                if (interactor != null)
                    _grabInteractable.interactionManager.SelectExit(interactor, _grabInteractable);
                // SelectExit → selectExited → OnDropped → IsHeld=false, InteractorTransform=null
            }
        }

        private void TryAttachToWall()
        {
            if (!_isTriggerHeld) return;
            if (_model == null) return;
            if (!_model.IsHeld) return; // 손에 들고 있을 때만
            if (!_isTouchingIce) return;
            if (_currentSurface == null) return;
            if (_model.IsAttachedToWall) return;

            // 컨트롤러의 실제 이동 속도로 스윙 세기 판정 (Velocity Damping 무관)
            float currentSqrSpeed = _controllerVelocity.sqrMagnitude;
            float minSqrVelocity = _minAttachVelocity * _minAttachVelocity;

            if (currentSqrSpeed < minSqrVelocity)
            {
                //Debug.Log($"[IceAxeController] 스윙 속도 부족. (컨트롤러 속도^2: {currentSqrSpeed:F2} < 요구 속도^2: {minSqrVelocity:F2}) 벽에 박히지 않습니다.");
                return;
            }

            //Debug.Log("[IceAxeController] 충돌 + 입력 조건 만족. 지형의 파괴 검사를 시작합니다.");

            bool allowAttachment = _currentSurface.OnHitByIceAxe(_contactPoint);

            AudioManager.instance.PlaySFX(AudioManager.SFXType.PickIce, transform);

            if (allowAttachment)
            {
                //Debug.Log("[IceAxeController] 검사 통과! Model에 벽면 부착 완료를 지시합니다.");
                // IsAttachedToWall = true를 먼저 세팅합니다.
                // IceAxeModel의 프로퍼티 세터가 OnAttachedStateChanged 이벤트를 동기적으로 발생시키고,
                // IceAxeView.HandleAttachedState(true)가 즉시 호출되어 XRI의 trackPosition = false가 됩니다.
                // trackPosition이 꺼진 이후에 SphereCast 스냅을 적용해야
                // XRI의 MovePosition()이 다음 FixedUpdate에서 보정값을 덮어쓰지 않습니다.
                _model.IsAttachedToWall = true;
                _depthCorrector?.TrySphereCastSnap();
            }
        }
    }
}
