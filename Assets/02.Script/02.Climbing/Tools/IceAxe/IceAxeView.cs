using UnityEngine;

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
                
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                CancelReturnCoroutine();
            }
            else
            {
                Debug.Log($"[IceAxeView - {_model.Side}] 벽에서 빠졌습니다. (물리 엔진 다시 가동)");
                
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

                transform.position = _pouchTransform.position;
                transform.rotation = _pouchTransform.rotation;
            }
        }
    }
}
