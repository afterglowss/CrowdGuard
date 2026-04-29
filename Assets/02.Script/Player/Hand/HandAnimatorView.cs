using UnityEngine;

namespace CrowdGuard.Player.Hand
{
    /// <summary>
    /// 손 애니메이션 객체의 시각적 업데이트를 담당하는 뷰 클래스입니다.
    /// 모델 데이터에 기반하여 Animator 컴포넌트의 파라미터를 조작합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HandAnimatorView : MonoBehaviour
    {
        private Animator _animator;
        private static readonly int GripParam = Animator.StringToHash("Grip");
        private static readonly int TriggerParam = Animator.StringToHash("Trigger");

        private bool _hasTriggerParam;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            // Animator Controller에 Trigger 파라미터가 있는지 확인
            _hasTriggerParam = false;
            if (_animator.runtimeAnimatorController != null)
            {
                foreach (var param in _animator.parameters)
                {
                    if (param.nameHash == TriggerParam)
                    {
                        _hasTriggerParam = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 제공된 모델 데이터를 기반으로 애니메이터 변수를 업데이트합니다.
        /// </summary>
        /// <param name="model">현재 그립/트리거 값을 가진 모델</param>
        public void UpdateAnimation(HandAnimatorModel model)
        {
            if (_animator == null || model == null) return;
            
            _animator.SetFloat(GripParam, model.GripValue);
            if (_hasTriggerParam)
            {
                _animator.SetFloat(TriggerParam, model.TriggerValue);
            }
        }
    }
}
