using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CrowdGuard.XR;

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
        [Tooltip("XR Gameplay Rig의 XRHapticManager를 연결")]
        [SerializeField] private XRHapticManager _hapticManager;
        [Tooltip("벽 타격 시 진동 강도 (0~1)")]
        [SerializeField][Range(0f, 1f)] private float _attachHapticAmplitude = 1.0f;
        [Tooltip("벽 타격 시 진동 지속 시간 (초)")]
        [SerializeField] private float _attachHapticDuration = 0.3f;

        [Header("파우치 시스템 (장착/복구)")]
        [Tooltip("플레이어 허리쯤에 위치할 빈 오브젝트(파우치 위치)를 연결")]
        [SerializeField] private Transform _pouchTransform;
        [Tooltip("허공에 떨어뜨리고 몇 초 뒤에 파우치로 돌아올지 결정")]
        [SerializeField] private float _autoReturnDelay = 3.0f;

        private Coroutine _returnCoroutine;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAxeModel>();
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
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
                // 잡았으므로 복구 타이머 취소
                CancelReturnCoroutine();
            }
            else
            {
                if (!_model.IsAttachedToWall)
                {
                    Debug.Log($"[IceAxeView - {_model.Side}] 허공에서 바일을 놓았습니다! (낙하 및 자동 복구 대기)");
                    _rb.useGravity = true;
                    _rb.isKinematic = false;

                    // 복구 타이머 시작
                    CancelReturnCoroutine();
                    _returnCoroutine = StartCoroutine(ReturnToPouchRoutine());
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
                CancelReturnCoroutine();
                SendHaptic(_attachHapticAmplitude, _attachHapticDuration);
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
                    CancelReturnCoroutine();
                    _returnCoroutine = StartCoroutine(ReturnToPouchRoutine());
                }
                else
                {
                    // 손에 쥐고 있다면 XR 툴킷이 물리제어를 하도록 중력을 끕니다
                    _rb.useGravity = false;
                }
            }
        }

        // ---------- Haptics ----------

        private void SendHaptic(float amplitude, float duration)
        {
            if (_grabInteractable == null)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: _grabInteractable == null");
                return;
            }
            if (_hapticManager == null)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: _hapticManager == null (인스펙터에서 연결 확인!)");
                return;
            }

            var interactors = _grabInteractable.interactorsSelecting;
            if (interactors.Count == 0)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: interactors.Count == 0 (잡고 있는 손 없음)");
                return;
            }

            var hapticPlayer = (interactors[0] as MonoBehaviour)?.GetComponentInParent<HapticImpulsePlayer>();
            if (hapticPlayer == null)
            {
                Debug.LogWarning("[IceAxeView] SendHaptic 실패: HapticImpulsePlayer를 컨트롤러 계층에서 찾을 수 없음");
                return;
            }

            Debug.Log($"[IceAxeView] SendHaptic 성공! amplitude={amplitude}, duration={duration}, player={hapticPlayer.gameObject.name}");
            _hapticManager.SendHaptic(hapticPlayer, amplitude, duration);
        }

        // ---------- Coroutines ----------

        private void CancelReturnCoroutine()
        {
            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
                _returnCoroutine = null;
            }
        }

        private System.Collections.IEnumerator ReturnToPouchRoutine()
        {
            // 정해진 시간 대기
            yield return new WaitForSeconds(_autoReturnDelay);

            // 시간이 끝났는데 여전히 잡지도 않고 박히지도 않았다면
            if (!_model.IsHeld && !_model.IsAttachedToWall && _pouchTransform != null)
            {
                Debug.Log("[IceAxeView] 코이! ");

                // 허리 위치로 순간이동
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;

                transform.SetPositionAndRotation(_pouchTransform.position, _pouchTransform.rotation);
            }
        }
    }
}
