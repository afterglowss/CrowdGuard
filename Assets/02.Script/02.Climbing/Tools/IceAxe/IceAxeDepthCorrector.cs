using UnityEngine;

namespace CrowdGuard.Climbing.Tools.IceAxe
{
    /// <summary>
    /// 아이스 바일이 벽에 너무 깊이 박히는 현상을 보정합니다.
    ///
    /// [기존 방식의 문제]
    /// 바일 중앙에 별도 body sensor를 두는 방식은, 실제 박히는 깊이(2~5cm)보다
    /// 센서가 팁에서 훨씬 멀리 있어 OverlapSphere가 아무것도 감지하지 못했습니다.
    ///
    /// [새 방식]
    /// IceAxeHead에 이미 붙어있는 팁 SphereCollider를 직접 참조합니다.
    /// Physics.ComputePenetration으로 팁이 벽에 얼마나 들어갔는지 정확하게 계산한 뒤
    /// 한 프레임 안에 바깥으로 꺼냅니다. 별도 자식 오브젝트나 콜라이더 추가 불필요.
    /// </summary>
    [RequireComponent(typeof(IceAxeModel), typeof(Rigidbody))]
    public class IceAxeDepthCorrector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IceAxeModel _model;
        [SerializeField] private Rigidbody _rb;

        [Tooltip("IceAxeHead 자식 오브젝트에 붙어있는 SphereCollider (isTrigger = true).\n" +
                 "새 오브젝트를 만들 필요 없이 기존 IceAxeHead의 콜라이더를 연결하세요.")]
        [SerializeField] private SphereCollider _tipCollider;

        [Header("Correction Settings")]
        [Tooltip("한 프레임에 밀어낼 최대 거리 (m).\n" +
                 "기본값 0.15로 두면 일반적인 박힘 깊이는 한 프레임에 즉시 보정됩니다.")]
        [SerializeField] private float _maxCorrectionPerFrame = 0.15f;

        // IceWall 레이어 고정
        private int _wallLayerMask;

        private void Awake()
        {
            if (_model == null) _model = GetComponent<IceAxeModel>();
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _wallLayerMask = LayerMask.GetMask("IceWall");
        }

        private void FixedUpdate()
        {
            if (_tipCollider == null || _model == null || !_model.IsAttachedToWall) return;

            Vector3 tipCenter  = _tipCollider.transform.TransformPoint(_tipCollider.center);
            float   tipRadius  = _tipCollider.radius * _tipCollider.transform.lossyScale.x;

            // 팁과 겹치는 IceWall 콜라이더를 탐색
            Collider[] overlapping = Physics.OverlapSphere(tipCenter, tipRadius, _wallLayerMask);
            if (overlapping.Length == 0) return;

            // ComputePenetration은 isTrigger=true 콜라이더를 지원하지 않으므로 잠깐 끄고 즉시 복원
            _tipCollider.isTrigger = false;

            Vector3 totalPush = Vector3.zero;
            foreach (Collider wallCol in overlapping)
            {
                if (Physics.ComputePenetration(
                        _tipCollider,  _tipCollider.transform.position,  _tipCollider.transform.rotation,
                        wallCol, wallCol.transform.position, wallCol.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                {
                    totalPush += pushDir * pushDist;
                }
            }

            _tipCollider.isTrigger = true;

            if (totalPush.sqrMagnitude < 1e-8f) return;

            // 한 프레임 최대 이동량 제한 후 보정 적용
            Vector3 correction = Vector3.ClampMagnitude(totalPush, _maxCorrectionPerFrame);

            _rb.constraints        = RigidbodyConstraints.None;
            _rb.velocity           = Vector3.zero;
            _rb.angularVelocity    = Vector3.zero;
            _rb.position          += correction;
            _rb.constraints        = RigidbodyConstraints.FreezeAll;
        }
    }
}
