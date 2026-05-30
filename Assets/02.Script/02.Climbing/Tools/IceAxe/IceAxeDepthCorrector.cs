using Fusion;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.IceAxe
{
    /// <summary>
    /// 아이스 바일이 벽에 너무 깊이 박히는 현상을 보정합니다.
    ///
    /// [왜 NetworkBehaviour인가]
    /// IceAxe에는 NetworkRigidbody3D가 붙어있습니다.
    /// NetworkRigidbody3D는 매 Fusion 틱 시작 시 저장된 네트워크 상태(박힌 위치)를 rb에 덮어씁니다.
    /// 따라서 Unity Update/FixedUpdate 타이밍에 위치를 수정해도 즉시 원복됩니다.
    /// FixedUpdateNetwork 안에서 보정을 적용하면, Fusion이 틱 종료 시
    /// 수정된 위치를 읽어 새 권위값으로 저장하므로 이후 틱에서도 유지됩니다.
    ///
    /// [주 보정 — SphereCast 스냅]
    /// OnTriggerEnter(팁이 벽에 닿는 순간) 타이밍에 OnTipContactBegin()을 호출해
    /// 그 시점의 _prevTipCenter(= 직전 FixedUpdate에서 기록된 벽 바깥 위치)를 보존합니다.
    /// 이후 IsAttachedToWall = true 직후 TrySphereCastSnap()을 호출하여
    /// "벽 밖 기점 → 현재 팁(벽 안)" SphereCast로 표면 접촉점을 역산하고,
    /// 보정값을 _pendingSnapOffset에 저장합니다.
    /// 다음 FixedUpdateNetwork에서 HasInputAuthority이면 보정을 적용하고
    /// Fusion이 그 위치를 새 권위값으로 가져갑니다.
    ///
    /// [안전망 — ComputePenetration]
    /// 부착 상태에서 FixedUpdateNetwork마다 잔류 침투를 감지해 보정값을 저장합니다.
    /// </summary>
    [RequireComponent(typeof(IceAxeModel), typeof(Rigidbody))]
    public class IceAxeDepthCorrector : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAxeModel _model;
        [SerializeField] private Rigidbody _rb;

        [Tooltip("IceAxeHead 자식 오브젝트에 붙어있는 SphereCollider (isTrigger = true).")]
        [SerializeField] private SphereCollider _tipCollider;

        [Header("Correction Settings")]
        [Tooltip("ComputePenetration 안전망에서 한 번에 밀어낼 최대 거리 (m).")]
        [SerializeField] private float _maxCorrectionPerFrame = 0.2f;

        [Tooltip("스냅 후 벽 표면에서 추가로 띄울 거리 (m).")]
        [SerializeField] private float _surfaceMargin = 0.015f;

        private int     _wallLayerMask;
        private Vector3 _prevTipCenter;
        private bool    _hasPrevTipCenter;

        private Vector3 _contactTimePrevTip;
        private bool    _hasContactTimePrev;

        // FixedUpdateNetwork에서 적용할 보정값 (NetworkRigidbody3D와 충돌 방지)
        private Vector3 _pendingSnapOffset;
        private bool    _hasPendingSnap;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAxeModel>();
            if (_rb == null)    _rb    = GetComponent<Rigidbody>();
            _wallLayerMask = LayerMask.GetMask("IceWall");
        }

        // FixedUpdate: physics step 이전에 팁 위치 기록 (Unity 타이밍 유지)
        private void FixedUpdate()
        {
            if (_tipCollider == null || _model == null) return;

            if (!_model.IsAttachedToWall)
            {
                _prevTipCenter    = _tipCollider.transform.TransformPoint(_tipCollider.center);
                _hasPrevTipCenter = true;
            }
        }

        // FixedUpdateNetwork: Fusion 틱 안에서 보정 적용
        // NetworkRigidbody3D가 틱 시작 시 저장 위치를 복원한 뒤 이 코드가 실행되고,
        // 틱 종료 시 Fusion이 수정된 위치를 새 권위값으로 저장합니다.
        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority) return;
            if (_tipCollider == null || _model == null) return;

            if (_hasPendingSnap)
            {
                ApplyOffset(_pendingSnapOffset);
                _hasPendingSnap = false;
            }
            else if (_model.IsAttachedToWall)
            {
                // 안전망: 잔류 침투가 있으면 다음 틱에 보정
                TryCorrectPenetration();
            }
        }

        // ── 주 보정: SphereCast 스냅 ─────────────────────────────────────

        public void OnTipContactBegin()
        {
            if (_hasPrevTipCenter)
            {
                _contactTimePrevTip = _prevTipCenter;
                _hasContactTimePrev = true;
            }
        }

        public void TrySphereCastSnap()
        {
            if (_tipCollider == null || _rb == null) return;

            Vector3 tipPos    = _tipCollider.transform.TransformPoint(_tipCollider.center);
            float   tipRadius = _tipCollider.radius * _tipCollider.transform.lossyScale.x;

            if (_hasContactTimePrev)
            {
                Vector3 sweep    = tipPos - _contactTimePrevTip;
                float   sweepLen = sweep.magnitude;

                if (sweepLen > 0.001f)
                {
                    Vector3 sweepDir = sweep / sweepLen;

                    if (Physics.SphereCast(
                            _contactTimePrevTip, tipRadius, sweepDir,
                            out RaycastHit hit, sweepLen, _wallLayerMask))
                    {
                        Vector3 tipAtContact = _contactTimePrevTip + sweepDir * hit.distance;
                        Vector3 snapOffset   = (tipAtContact - tipPos) + hit.normal * _surfaceMargin;

                        // Debug.Log($"[DepthCorrector] SphereCast 성공 — offset={snapOffset.magnitude:F4}m");
                        _pendingSnapOffset = snapOffset;
                        _hasPendingSnap    = true;
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"[DepthCorrector] SphereCast miss — ComputePenetration으로 폴백");
                    }
                }
            }

            TryCorrectPenetration();
        }

        // ── 안전망: ComputePenetration ───────────────────────────────────

        public void TryCorrectPenetration()
        {
            if (_tipCollider == null || _rb == null) return;

            Vector3 tipCenter = _tipCollider.transform.TransformPoint(_tipCollider.center);
            float   tipRadius = _tipCollider.radius * _tipCollider.transform.lossyScale.x;

            Collider[] overlapping = Physics.OverlapSphere(tipCenter, tipRadius, _wallLayerMask);
            if (overlapping.Length == 0) return;

            _tipCollider.isTrigger = false;

            Vector3 totalPush = Vector3.zero;
            foreach (Collider wallCol in overlapping)
            {
                if (Physics.ComputePenetration(
                        _tipCollider, _tipCollider.transform.position, _tipCollider.transform.rotation,
                        wallCol,      wallCol.transform.position,      wallCol.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                {
                    totalPush += pushDir * pushDist;
                }
            }

            _tipCollider.isTrigger = true;

            if (totalPush.sqrMagnitude < 1e-8f) return;

            Vector3 correction = Vector3.ClampMagnitude(
                totalPush + totalPush.normalized * _surfaceMargin,
                _maxCorrectionPerFrame);

            _pendingSnapOffset = correction;
            _hasPendingSnap    = true;
        }

        // ── 공통 이동 적용 (FixedUpdateNetwork 안에서만 호출) ─────────────

        private void ApplyOffset(Vector3 offset)
        {
            if (offset.sqrMagnitude < 1e-8f) return;

            bool wasFrozen = _rb.constraints == RigidbodyConstraints.FreezeAll;
            if (wasFrozen)
                _rb.constraints = RigidbodyConstraints.None;

            _rb.velocity        = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.position += offset;
            Physics.SyncTransforms();

            if (wasFrozen)
                _rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
}
