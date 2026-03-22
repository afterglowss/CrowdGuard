using UnityEngine;
using UnityEngine.InputSystem;
using CrowdGuard.Environment;
using UnityEngine.XR.Interaction.Toolkit; 

namespace CrowdGuard.Climbing.Tools.IceAxe
{

    [RequireComponent(typeof(IceAxeModel), typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class IceAxeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAxeModel _model;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;

        [Header("Physics & Rules")]
        [Tooltip("아이스 바일이 벽에 박히기 위해 필요한 최소 속도")]
        [SerializeField] private float _minAttachVelocity = 1.5f;

        private Rigidbody _rb;

        private bool _isTriggerHeld = false;
        private bool _isTouchingIce = false;
        private ClimbableSurface _currentSurface = null;

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
            Debug.Log("[IceAxeController] XRI 그랩 발동 - 플레이어가 손으로 바일을 쥐었습니다!");
            if (_model != null) _model.IsHeld = true;
        }

        private void OnDropped(SelectExitEventArgs args)
        {
            Debug.Log("[IceAxeController] XRI 그랩 해제 - 플레이어가 손에서 바일을 놓았습니다.");
            if (_model != null) _model.IsHeld = false;
        }


        private void OnTriggerActivated(ActivateEventArgs args)
        {
            Debug.Log("[IceAxeController] XRI Activate (Trigger) Pressed - 장비 고정 의도 (Trigger 유지 시작)");
            _isTriggerHeld = true;
            TryAttachToWall();
        }

        private void OnTriggerDeactivated(DeactivateEventArgs args)
        {
            Debug.Log("[IceAxeController] XRI Deactivate (Trigger) Released - 얼음벽에서 바일 분리");
            _isTriggerHeld = false;
            if (_model != null) _model.IsAttachedToWall = false;
        }


        public void OnIceContactEnter(ClimbableSurface surface)
        {
            if (surface.Type == SurfaceType.Rock)
            {
                Debug.Log("[IceAxeController] 단단한 바위에 부딪혀 바일이 박히지 않습니다.");
                return;
            }

            Debug.Log("[IceAxeController] 박힐 가능성이 있는 얼음벽 표면에 닿았습니다.");
            _isTouchingIce = true;
            _currentSurface = surface;
            TryAttachToWall();
        }

        public void OnIceContactExit(ClimbableSurface surface)
        {
            if (_currentSurface == surface)
            {
                Debug.Log("[IceAxeController] 바일 머리가 얼음벽에서 떨어졌습니다.");
                _isTouchingIce = false;
                _currentSurface = null;
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

            // 추가: 유저가 꽉 쥐고 세게 내리쳤는가? 속도 검사
            if (_rb != null)
            {
                float currentSqrSpeed = _rb.velocity.sqrMagnitude;
                float minSqrVelocity = _minAttachVelocity * _minAttachVelocity;
                
                if (currentSqrSpeed < minSqrVelocity)
                {
                    Debug.Log($"[IceAxeController] 스윙 속도 부족. (현재 속도^2: {currentSqrSpeed:F2} < 요구 속도^2: {minSqrVelocity:F2}) 벽에 박히지 않습니다.");
                    return; 
                }
            }

            Debug.Log("[IceAxeController] 충돌 + 입력 조건 만족. 지형의 파괴 검사를 시작합니다.");
            
            bool allowAttachment = _currentSurface.OnHitByIceAxe();

            if (allowAttachment)
            {
                Debug.Log("[IceAxeController] 검사 통과! Model에 벽면 부착 완료를 지시합니다.");
                _model.IsAttachedToWall = true;
            }
        }
    }
}
