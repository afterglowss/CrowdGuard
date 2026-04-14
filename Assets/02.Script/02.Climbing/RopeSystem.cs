using UnityEngine;
using System;
using Fusion;
using GogoGaga.OptimizedRopesAndCables;

// [RequireComponent(typeof(LineRenderer))] <- 기존의 이 줄은 지워주세요!
public class RopeSystem : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("체크하면 로프 시스템을 완전히 비활성화합니다. (나홀로 등반 테스트용)")]
    public bool disableRopeForTesting = false;

    [Header("Rope Settings")]
    public Transform myBodyTransform;
    public Transform partnerTransform;
    public Vector3 localTieOffset = new Vector3(0, -0.5f, 0);
    public float maxRopeLength = 3.0f;

    [Header("Asset Reference")]
    [Tooltip("방금 만든 VisualRope 오브젝트를 여기에 넣으세요.")]
    // 👇 [수정] MonoBehaviour 대신 명확하게 Rope 타입으로 변경합니다!
    public Rope assetRope;

    // 기존에 있던 Visuals (Curve), LineRenderer 관련 변수들은 모두 삭제합니다!

    private Transform dummyTransform;

    private System.Collections.IEnumerator Start()
    {
        if (disableRopeForTesting)
        {
            Debug.Log("[RopeSystem] 테스트용 토글이 켜져 있어 로프 시스템이 작동하지 않습니다.");
            yield break; 
        }

        // 1. 카메라(내 몸통) 실시간 찾기
        if (myBodyTransform == null && Camera.main != null)
        {
            myBodyTransform = Camera.main.transform;
        }

        // 👇 [핵심 수정] 더미 모드인지 멀티 모드인지 먼저 판별합니다.
        GameObject dummy = GameObject.Find("DummyPartner");

        // 씬에 더미가 켜져있다면 -> "혼자 테스트하는 중이구나!" (기다리지 않고 더미 연결)
        if (dummy != null && dummy.activeInHierarchy)
        {
            partnerTransform = dummy.transform;
            Debug.Log("[RopeSystem] 더미 파트너와 연결되었습니다. (솔로 테스트 모드)");
        }
        else
        {
            // 씬에 더미가 없다면 -> "멀티플레이 상황이구나!" (2P가 올 때까지 무한 대기)
            Debug.Log("[RopeSystem] 멀티플레이어 접속 대기 중...");

            while (partnerTransform == null)
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

                foreach (var p in players)
                {
                    if (p.transform.root != this.transform.root)
                    {
                        partnerTransform = p.transform;
                        Debug.Log("[RopeSystem] 드디어 네트워크 파트너를 찾았습니다!");
                        break;
                    }
                }

                if (partnerTransform == null)
                {
                    yield return new WaitForSeconds(0.5f); // 0.5초마다 재확인
                }
            }
        }

        // 3. 파트너를 찾았다면, 에셋(VisualRope)이 세팅될 수 있도록 1프레임 양보
        yield return null;

        // 4. 에셋 묶기
        AttachRopeToAsset();
    }

    private void AttachRopeToAsset()
    {
        if (assetRope == null || myBodyTransform == null || partnerTransform == null) return;

        // 에셋(Rope.cs)이 제공하는 함수를 호출하여 내 몸과 파트너를 양 끝단에 묶어버립니다!
        // (true를 넣으면 즉시 밧줄이 해당 위치로 이동하여 계산을 시작합니다)
        assetRope.SetStartPoint(myBodyTransform, true);
        assetRope.SetEndPoint(partnerTransform, true);

        Debug.Log("[RopeSystem] 시각적 에셋 로프가 양 플레이어에게 성공적으로 연결되었습니다!");
    }

    // 기존의 Update()에 있던 베지어 곡선 그리기 로직은 전부 삭제합니다! (에셋이 대신 해줌)

    // 👇 구면 교차(Ray-Sphere Intersection)를 적용한 완벽한 텐션 제한 로직
    public void LimitMovement(ref Vector3 proposedDeltaWorld)
    {
        if (disableRopeForTesting) return;

        if (partnerTransform == null || myBodyTransform == null) return;

        Vector3 myTiePoint = myBodyTransform.TransformPoint(localTieOffset);
        Vector3 partnerPos = partnerTransform.position;

        Vector3 predictedPos = myTiePoint - proposedDeltaWorld;

        float currentDistance = Vector3.Distance(myTiePoint, partnerPos);
        float predictedDistance = Vector3.Distance(predictedPos, partnerPos);

        // 로프 길이를 초과하려고 하며, 동시에 현재보다 더 멀어지려고 할 때만 막습니다.
        if (predictedDistance > maxRopeLength && predictedDistance > currentDistance)
        {
            // 이미 로프가 팽팽한(최대 길이 도달) 상태에서 더 멀어지려 하면, 이동을 아예 차단합니다! (벽에서 안 밀려남)
            if (currentDistance >= maxRopeLength)
            {
                proposedDeltaWorld = Vector3.zero;
                return;
            }

            // --- Ray-Sphere Intersection (구면 교차 연산) ---
            // 플레이어가 의도한 방향(V)으로 이동할 때, 구의 표면(maxRopeLength)에 닿을 때까지의
            // '허용 가능한 이동 비율(t)'을 구하는 2차 방정식입니다.

            Vector3 V = -proposedDeltaWorld;
            Vector3 L = myTiePoint - partnerPos;

            float a = Vector3.Dot(V, V);
            if (a < 0.0001f) return; // 미세한 움직임 무시

            float b = 2f * Vector3.Dot(V, L);
            float c = Vector3.Dot(L, L) - (maxRopeLength * maxRopeLength);

            float discriminant = (b * b) - (4f * a * c);

            if (discriminant >= 0)
            {
                // 0 ~ 1 사이의 허용 비율 t를 구합니다.
                float t = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
                t = Mathf.Clamp01(t);

                // 플레이어가 의도한 이동 방향은 그대로 두고, 이동량만 t만큼 축소시킵니다!
                proposedDeltaWorld *= t;
            }
            else
            {
                // 예외 상황 방어
                proposedDeltaWorld = Vector3.zero;
            }
        }
    }
}