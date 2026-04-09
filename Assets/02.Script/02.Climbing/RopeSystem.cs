using UnityEngine;
using System;
using Fusion;
using GogoGaga.OptimizedRopesAndCables;

// [RequireComponent(typeof(LineRenderer))] <- 기존의 이 줄은 지워주세요!
public class RopeSystem : MonoBehaviour
{
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
        // 1. 카메라(내 몸통) 실시간 찾기
        if (myBodyTransform == null && Camera.main != null)
        {
            myBodyTransform = Camera.main.transform;
        }

        // 2. 파트너가 네트워크를 타고 스폰될 때까지 인내심 있게 기다립니다.
        while (partnerTransform == null)
        {
            // 씬에 있는 모든 'Player' 태그를 가진 오브젝트를 찾습니다.
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

            foreach (var p in players)
            {
                // 찾은 플레이어가 '나 자신'이 아니라면? 그 사람이 바로 내 파트너입니다!
                if (p.transform.root != this.transform.root)
                {
                    partnerTransform = p.transform;
                    Debug.Log("[RopeSystem] 드디어 네트워크 파트너를 찾았습니다!");
                    break;
                }
            }

            // 멀티 접속 전 혼자 테스트할 때를 위한 Dummy 방어 코드 유지
            if (partnerTransform == null)
            {
                GameObject dummy = GameObject.Find("DummyPartner");
                if (dummy != null) partnerTransform = dummy.transform;
            }

            // 여전히 파트너를 못 찾았다면, 0.5초 대기 후 다시 찾습니다. (과부하 방지)
            if (partnerTransform == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        // 3. 파트너를 무사히 찾았다면, 에셋(VisualRope)이 세팅될 수 있도록 딱 1프레임만 더 양보합니다.
        yield return null;

        // 4. 에셋에 시작점과 끝점 묶어주기!
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