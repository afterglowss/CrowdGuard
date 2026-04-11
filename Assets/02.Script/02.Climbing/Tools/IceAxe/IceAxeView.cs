using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CrowdGuard.XR;
using CrowdGuard.Climbing.Tools.Common;

namespace CrowdGuard.Climbing.Tools.IceAxe
{
    /// <summary>
    /// </summary>
    [RequireComponent(typeof(IceAxeModel), typeof(Rigidbody))]
    public class IceAxeView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAxeModel _model;
        [SerializeField] private Rigidbody _rb;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;

        [Header("Haptics (진동 피드백)")]
        [Tooltip("빙벽 타격 시 재생할 햅틱 프로파일 (에셋)")]
        [SerializeField] private CrowdGuard.XR.Haptics.HapticProfile _onAttachHaptic;

        private RetractableObject _retractableObject;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAxeModel>();
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            _retractableObject = GetComponent<RetractableObject>();
        }

        private void OnEnable()
        {
            if (_model != null)
            {
                _model.OnHeldStateChanged += HandleHeldState;
                _model.OnAttachedStateChanged += HandleAttachedState;
            }
        }

        private void OnDisable()
        {
            if (_model != null)
            {
                _model.OnHeldStateChanged -= HandleHeldState;
                _model.OnAttachedStateChanged -= HandleAttachedState;
            }
        }


        private void HandleHeldState(bool isHeld)
        {
            if (isHeld)
            {
                Debug.Log($"[IceAxeView - {_model.Side}] 손에 장착되었습니다. (컨트롤러 Transform 매칭 시작)");
                _rb.useGravity = false;
                _rb.isKinematic = false;
                // 잡았으므로 복구 타이머 취소 (RetractableObject가 자체적으로 처리)
            }
            else
            {
                if (!_model.IsAttachedToWall)
                {
                    Debug.Log($"[IceAxeView - {_model.Side}] 허공에서 바일을 놓았습니다! (낙하 및 자동 복구 대기)");
                    _rb.useGravity = true;
                    _rb.isKinematic = false;
                }
            }
        }

        private void HandleAttachedState(bool isAttached)
        {
            if (isAttached)
            {
                Debug.Log($"[IceAxeView] {_model.Side} 벽에 박혔습니다. ");

                // XRI의 위치/회전 추적 비활성화 (Kinematic 모드에서 벽 고정)
                if (_grabInteractable != null)
                {
                    _grabInteractable.trackPosition = false;
                    _grabInteractable.trackRotation = false;
                }
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                if (_retractableObject != null) _retractableObject.CancelReturn();
                SendHaptic();
            }
            else
            {
                Debug.Log($"[IceAxeView - {_model.Side}] 벽에서 빠졌습니다. (물리 엔진 다시 가동)");

                // XRI 추적 복원
                if (_grabInteractable != null)
                {
                    _grabInteractable.trackPosition = true;
                    _grabInteractable.trackRotation = true;
                }

                // 물리 잠금(축 얼림) 해제
                _rb.constraints = RigidbodyConstraints.None;

                if (!_model.IsHeld)
                {
                    _rb.useGravity = true;

                    // 벽에서 빠졌는데 잡고 있지도 않다면 다시 추락 타이머 가동
                    if (_retractableObject != null) _retractableObject.RequestReturn();
                }
                else
                {
                    // 손에 쥐고 있다면 XR 툴킷이 물리제어를 하도록 중력을 끕니다
                    _rb.useGravity = false;
                }
            }
        }

        // ---------- Haptics ----------

        private void SendHaptic()
        {
            if (_grabInteractable == null)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: _grabInteractable == null");
                return;
            }
            if (_onAttachHaptic == null)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: _onAttachHaptic 프로파일이 연결되지 않음");
                return;
            }

            var interactors = _grabInteractable.interactorsSelecting;
            if (interactors.Count == 0)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: interactors.Count == 0 (잡고 있는 손 없음)");
                return;
            }

            var provider = (interactors[0] as MonoBehaviour)?.GetComponentInParent<CrowdGuard.XR.Haptics.IHapticProvider>();
            if (provider == null)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: 이 컨트롤러/인터랙터 계층에서 IHapticProvider를 찾을 수 없음");
                return;
            }

            Debug.Log($"[IceAxeView] SendHaptic: IceAxeAttach, provider={((MonoBehaviour)provider).gameObject.name}");
            provider.PlayHaptic(_onAttachHaptic);
        }

    }
}
