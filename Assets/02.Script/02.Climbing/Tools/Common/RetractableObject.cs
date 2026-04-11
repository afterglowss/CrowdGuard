using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CrowdGuard.Climbing.Tools.Common
{
    /// <summary>
    /// XRGrabInteractable이 있는 도구에 부착하여 파우치 자동 복귀 기능을 제공합니다.
    /// 놓으면(SelectExited) 딜레이 후 지정된 파우치 위치로 되돌아갑니다.
    /// 벽에 박혔을 때 복귀를 취소하려면 외부 컴포넌트(IceAxe 등)에서 CancelReturn()을 호출해야 합니다.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class RetractableObject : MonoBehaviour
    {
        [Header("Retract Settings")]
        [Tooltip("도구가 돌아갈 파우치의 위치 (빈 Transform)")]
        public Transform pouchTransform;
        
        [Tooltip("놓은 후 복귀 시작까지의 대기 시간 (초)")]
        [SerializeField] private float _autoReturnDelay = 3f;
        
        [Tooltip("파우치로 이동하는 속도")]
        [SerializeField] private float _autoReturnSpeed = 10f;

        private XRGrabInteractable _grabInteractable;
        private Rigidbody _rb;
        private Coroutine _returnCoroutine;

        private void Awake()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnDropped);
        }

        private void OnDisable()
        {
            _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            _grabInteractable.selectExited.RemoveListener(OnDropped);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            // 잡힐 때 부모(파우치)로부터 분리
            transform.SetParent(null);
            CancelReturn();
        }

        private void OnDropped(SelectExitEventArgs args)
        {
            // 놓을 때 복귀 요청
            RequestReturn();
        }

        /// <summary>
        /// 파우치 복귀 타이머를 작동시킵니다.
        /// (벽 등에서 떨어지거나, 잡고 있던 것을 놓을 때 호출)
        /// </summary>
        public void RequestReturn()
        {
            if (pouchTransform == null) return;
            
            // grab 상태면 복귀하지 않음 (이중 체크)
            if (_grabInteractable.isSelected) return;

            CancelReturn();
            _returnCoroutine = StartCoroutine(ReturnToPouchRoutine());
        }

        /// <summary>
        /// 진행 중인 파우치 복귀(또는 대기)를 즉시 취소합니다.
        /// IceAxeView 등이 벽에 박혔을 때 호출합니다.
        /// </summary>
        public void CancelReturn()
        {
            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
                _returnCoroutine = null;
            }
        }

        private IEnumerator ReturnToPouchRoutine()
        {
            yield return new WaitForSeconds(_autoReturnDelay);

            if (!_grabInteractable.isSelected && pouchTransform != null)
            {
                Debug.Log($"[{gameObject.name}] 파우치로 복귀합니다.");

                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
                _rb.useGravity = false;

                while (Vector3.Distance(transform.position, pouchTransform.position) > 0.01f)
                {
                    // 이동 중에 갑자기 잡히면 정지
                    if (_grabInteractable.isSelected) yield break;

                    transform.position = Vector3.Lerp(transform.position, pouchTransform.position, Time.deltaTime * _autoReturnSpeed);
                    transform.rotation = Quaternion.Slerp(transform.rotation, pouchTransform.rotation, Time.deltaTime * _autoReturnSpeed);
                    yield return null;
                }

                transform.SetPositionAndRotation(pouchTransform.position, pouchTransform.rotation);
                transform.SetParent(pouchTransform);
            }
        }
    }
}
