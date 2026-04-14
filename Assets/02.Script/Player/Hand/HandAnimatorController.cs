using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdGuard.Player.Hand
{
    /// <summary>
    /// 손 애니메이션 객체를 위한 컨트롤러 클래스입니다.
    /// Input Action으로부터 값을 읽어들여 모델을 업데이트하고, 뷰를 통해 애니메이션을 반영합니다.
    /// </summary>
    public class HandAnimatorController : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField, Tooltip("그립(Grip) 값을 읽어올 InputActionReference")]
        private InputActionReference _gripAction;
        
        [SerializeField, Tooltip("트리거(Trigger) 값을 읽어올 InputActionReference")]
        private InputActionReference _triggerAction;

        [Header("References")]
        [SerializeField, Tooltip("시각적 애니메이션 업데이트를 담당하는 뷰 컴포넌트")]
        private HandAnimatorView _handAnimatorView;

        [Header("Debug")]
        [SerializeField, Tooltip("입력값을 주기적으로 출력할지 여부")]
        private bool _enableDebugLog = false;

        private HandAnimatorModel _model;

        private void Awake()
        {
            _model = new HandAnimatorModel();

            if (_handAnimatorView == null)
            {
                _handAnimatorView = GetComponent<HandAnimatorView>();
            }
        }

        private void Update()
        {
            // 컨트롤러 입력 (Grip, Trigger) 상태 읽기
            float grip = 0f;
            if (_gripAction != null && _gripAction.action != null && _gripAction.action.enabled)
            {
                grip = _gripAction.action.ReadValue<float>();
            }

            float trigger = 0f;
            if (_triggerAction != null && _triggerAction.action != null && _triggerAction.action.enabled)
            {
                trigger = _triggerAction.action.ReadValue<float>();
            }

            // 모델 값 갱신
            _model.GripValue = grip;
            _model.TriggerValue = trigger;

            // 뷰 업데이트 수행
            if (_handAnimatorView != null)
            {
                _handAnimatorView.UpdateAnimation(_model);
            }

            if (_enableDebugLog)
            {
                Debug.Log($"[{gameObject.name}] Grip: {grip:F2} | Trigger: {trigger:F2}");
            }
        }
    }
}
