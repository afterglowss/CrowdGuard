using Fusion;
using SimpleAudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CrowdGuard.Environment
{
    public class WeakIceSurface : BaseSurface
    {
        [Header("Weak Ice Settings")]
        [SerializeField, Range(0f, 1f)] private float breakProbability = 0.8f;

        [Header("Fracture Physics")]
        [Tooltip("파편에 가할 폭발력. 클수록 파편이 강하게 흩어짐")]
        [SerializeField] private float explosionForce = 400f;
        [Tooltip("폭발 반경. 얼음 덩어리 크기에 맞게 조절")]
        [SerializeField] private float explosionRadius = 3f;
        [Tooltip("파편이 위로 솟아오르는 정도 (0=수평, 1=위로)")]
        [SerializeField, Range(0f, 2f)] private float explosionUpward = 0.5f;

        // 파괴된 자식의 sibling index 추적 (RPC로 양쪽 클라이언트 동기화)
        // index = -1 은 자식 없이 본체가 파괴된 경우
        private readonly HashSet<int> _brokenChildren = new HashSet<int>();

        public override bool OnHitByIceAxe(Vector3 contactPoint = default)
        {
            int idx = FindChildIndexAtPoint(contactPoint);

            if (idx >= 0 && _brokenChildren.Contains(idx)) return false;
            if (idx < 0 && _brokenChildren.Contains(-1)) return false;

            float roll = Random.Range(0f, 1f);
            if (roll <= breakProbability)
            {
                Debug.Log($"[WeakIceSurface] 약한 얼음 파괴! childIdx={idx}");
                RPC_BreakChild(idx);

                if (Random.Range(0f, 1f) <= 0.5f)
                {
                    HazardData rockfallData = new RockfallData
                    {
                        Location = transform.position,
                        RockCount = 3,
                        FallRadius = 2f
                    };
                    HazardManager.Instance?.TriggerHazardExternal(rockfallData);
                }

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

                float dist = col != null
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

            Fracture fracture = target.GetComponent<Fracture>();
            if (fracture != null)
            {
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                fracture.CauseFracture();
                StartCoroutine(ApplyExplosionForce(target, fracture.fractureOptions.asynchronous));
            }
            else
            {
                Debug.LogWarning($"[WeakIceSurface] {target.name}에 Fracture 컴포넌트 없음 → SetActive(false) 폴백");
                target.SetActive(false);
            }
        }

        private IEnumerator ApplyExplosionForce(GameObject fracturedTarget, bool isAsync)
        {
            if (isAsync)
                yield return new WaitForSeconds(0.15f);
            else
                yield return null;

            string fragmentRootName = fracturedTarget.name + "Fragments";
            Transform searchParent = fracturedTarget.transform.parent ?? transform.parent;
            if (searchParent == null) yield break;

            Vector3 blastCenter = fracturedTarget.transform.position;

            foreach (Transform child in searchParent)
            {
                if (child.name != fragmentRootName) continue;

                foreach (Transform fragment in child)
                {
                    var rb = fragment.GetComponent<Rigidbody>();
                    if (rb == null) continue;
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    rb.AddExplosionForce(explosionForce, blastCenter, explosionRadius,
                                         explosionUpward, ForceMode.Impulse);
                }

                // 2초 후 파편 루트 제거
                Destroy(child.gameObject, 2f);
                break;
            }
        }

        protected override void OnBrokenChanged() { }
    }
}
