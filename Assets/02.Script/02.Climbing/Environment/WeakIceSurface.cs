using Fusion;
using SimpleAudioManager;
using System.Collections.Generic;
using UnityEngine;

namespace CrowdGuard.Environment
{
    public class WeakIceSurface : BaseSurface
    {
        [Header("Weak Ice Settings")]
        [SerializeField, Range(0f, 1f)] private float breakProbability = 0.8f;

        [Header("Rockfall")]
        [Tooltip("얼음 파괴 시 발동할 HazardManager.rockSystems 인덱스. -1이면 낙석 없음.")]
        [SerializeField] private int _rockfallIndex = -1;

        [Header("Fracture Physics")]
        [Tooltip("파편에 가할 폭발력. 클수록 파편이 강하게 흩어짐")]
        [SerializeField] private float explosionForce = 400f;
        [Tooltip("폭발 반경. 얼음 덩어리 크기에 맞게 조절")]
        [SerializeField] private float explosionRadius = 3f;
        [Tooltip("파편이 위로 솟아오르는 정도 (0=수평, 1=위로)")]
        [SerializeField, Range(0f, 2f)] private float explosionUpward = 0.5f;

        // 파괴된 자식의 sibling index 추적 (RPC로 접속 중인 클라이언트끼리 즉시 동기화)
        // index = -1 은 자식 없이 본체가 파괴된 경우
        private readonly HashSet<int> _brokenChildren = new HashSet<int>();

        // 재접속/늦은 입장 클라이언트가 파괴 상태를 복원할 수 있도록 깨진 자식을 비트마스크로 동기화.
        // bit i = 자식 i 파괴, 최상위 비트(1<<31) = 자식 없는 본체(idx -1) 파괴.
        // (자식이 31개를 넘으면 마스크가 부족하므로 그 이상은 지원하지 않음)
        [Networked, OnChangedRender(nameof(OnBrokenMaskChanged))]
        public int BrokenMask { get; set; }

        private const int SelfBit = unchecked((int)(1u << 31));
        private static int BitFor(int idx) => idx < 0 ? SelfBit : (1 << idx);

        public override bool OnHitByIceAxe(Vector3 contactPoint = default, Vector3 contactNormal = default)
        {
            int idx = FindChildIndexAtPoint(contactPoint);

            if (idx >= 0 && _brokenChildren.Contains(idx)) return false;
            if (idx < 0 && _brokenChildren.Contains(-1)) return false;

            float roll = Random.Range(0f, 1f);
            if (roll <= breakProbability)
            {
                Debug.Log($"[WeakIceSurface] 약한 얼음 파괴! childIdx={idx}");
                RPC_BreakChild(idx);

                if (Random.Range(0f, 1f) <= 0.5f && _rockfallIndex >= 0)
                    HazardManager.Instance?.RPC_TriggerRockfall(_rockfallIndex);

                return false;
            }

            return true;
        }

        public override bool CanInstallAnchor(Vector3 contactPoint = default)
        {
            int idx = FindChildIndexAtPoint(contactPoint);
            if (idx >= 0 && _brokenChildren.Contains(idx)) return false;
            if (idx < 0 && _brokenChildren.Contains(-1)) return false;
            return true;
        }

        public override bool IsBrokenAt(Vector3 worldPoint)
        {
            int idx = FindChildIndexAtPoint(worldPoint);
            return idx >= 0 ? _brokenChildren.Contains(idx) : _brokenChildren.Contains(-1);
        }

        /// <summary>
        /// contactPoint에 가장 가까운 직계 자식의 sibling index를 반환합니다.
        /// 자식이 없으면 -1 (본체가 파괴 대상).
        /// </summary>
        private int FindChildIndexAtPoint(Vector3 worldPoint)
        {
            if (transform.childCount == 0) return -1;

            int closest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Collider col = child.GetComponent<Collider>();

                // 주의: Collider.ClosestPoint는 "비활성 콜라이더/비활성 오브젝트"에 호출하면
                // 입력 좌표를 그대로 돌려줘 거리 0이 된다. 파괴된 자식(CauseFracture가
                // SetActive(false) 처리)이 이 때문에 항상 "가장 가까운 자식"으로 잘못 선택되어
                // 이후 모든 타격이 깨진 자식으로 매핑되는 버그가 생긴다.
                // → 콜라이더가 실제로 유효할 때만 ClosestPoint를 쓰고, 아니면 중심 좌표로 거리 계산.
                bool colliderUsable = col != null && col.enabled && child.gameObject.activeInHierarchy;

                float dist = colliderUsable
                    ? Vector3.Distance(col.ClosestPoint(worldPoint), worldPoint)
                    : Vector3.Distance(child.position, worldPoint);

                if (dist < minDist)
                {
                    minDist = dist;
                    closest = i;
                }
            }

            return closest;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_BreakChild(int childIndex)
        {
            if (!_brokenChildren.Add(childIndex)) return; // 이미 파괴됨

            GameObject target = (childIndex >= 0 && childIndex < transform.childCount)
                ? transform.GetChild(childIndex).gameObject
                : gameObject;

            Debug.Log($"[WeakIceSurface] RPC_BreakChild → target={target.name}");

            if (breakVFX != null)
                Instantiate(breakVFX, target.transform.position, Quaternion.identity);

            AudioManager.instance.PlaySFX(AudioManager.SFXType.IceBreak, transform);

            // Fusion 네트워크 상태 갱신: 재접속 클라이언트가 "어떤 자식이" 깨졌는지 복원할 수 있도록
            // 깨진 자식 비트만 켠다. (부모 전체를 Despawn하지 않으므로 나머지 자식은 그대로 유지)
            if (Object.HasStateAuthority)
                BrokenMask |= BitFor(childIndex);

            Fracture fracture = target.GetComponent<Fracture>();
            if (fracture != null)
            {
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                // async 코루틴이 Fusion 상태 동기화와 레이스 컨디션을 일으키는 것을 방지
                fracture.fractureOptions.asynchronous = false;
                fracture.CauseFracture(); // sync이므로 반환 시 파편 생성 완료
                ApplyExplosionForce(target);
            }
            else
            {
                Debug.LogWarning($"[WeakIceSurface] {target.name}에 Fracture 컴포넌트 없음 → SetActive(false) 폴백");
                target.SetActive(false);
            }
        }

        private void ApplyExplosionForce(GameObject fracturedTarget)
        {
            string fragmentRootName = fracturedTarget.name + "Fragments";
            Transform searchParent = fracturedTarget.transform.parent ?? transform.parent;
            if (searchParent == null) return;

            Vector3 blastCenter = fracturedTarget.transform.position;

            foreach (Transform child in searchParent)
            {
                if (child.name != fragmentRootName) continue;

                // OnBrokenChanged → WeakIceSurface.SetActive(false) 시 fragmentRoot가
                // 자식으로 같이 꺼지지 않도록 씬 루트로 분리
                child.SetParent(null);

                foreach (Transform fragment in child)
                {
                    var rb = fragment.GetComponent<Rigidbody>();
                    if (rb == null) continue;
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    // ForceMode.Impulse는 "힘 ÷ 질량"이라, 작게 쪼개진 파편(질량이 작음)이
                    // 같은 explosionForce에도 엄청난 속도를 받아 하늘로 솟구친다.
                    // ForceMode.VelocityChange는 질량과 무관하게 동일한 속도 변화를 주므로
                    // 파편 크기에 상관없이 일정하게 흩어진다. (explosionForce는 이제 m/s 단위로 해석)
                    rb.AddExplosionForce(explosionForce, blastCenter, explosionRadius,
                                         explosionUpward, ForceMode.VelocityChange);
                }

                Destroy(child.gameObject, 2f);
                break;
            }
        }

        // 늦게 입장한 클라이언트는 초기 네트워크 상태(BrokenMask)를 받지만 OnChangedRender는
        // "변경" 시에만 호출되므로, Spawned 시점에 한 번 명시적으로 동기화한다.
        public override void Spawned()
        {
            if (BrokenMask != 0)
                ReconcileBrokenMask();
        }

        private void OnBrokenMaskChanged()
        {
            ReconcileBrokenMask();
        }

        // 네트워크 마스크와 로컬 상태를 비교해, 아직 로컬에서 깨지지 않은 자식만 조용히 파괴 상태로 맞춘다.
        // 재접속 클라이언트는 원래의 RPC_BreakChild를 받지 못했으므로 파편 연출 없이 숨김 처리한다.
        // (RPC를 이미 처리한 클라이언트는 _brokenChildren에 들어있어 중복 처리되지 않음)
        private void ReconcileBrokenMask()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if ((BrokenMask & (1 << i)) != 0 && _brokenChildren.Add(i))
                    HideChildSilently(i);
            }

            if ((BrokenMask & SelfBit) != 0 && _brokenChildren.Add(-1))
                HideChildSilently(-1);
        }

        private void HideChildSilently(int childIndex)
        {
            GameObject target = (childIndex >= 0 && childIndex < transform.childCount)
                ? transform.GetChild(childIndex).gameObject
                : gameObject;
            target.SetActive(false);
        }
    }
}
