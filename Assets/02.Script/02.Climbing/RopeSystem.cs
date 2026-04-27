using UnityEngine;
using GogoGaga.OptimizedRopesAndCables;

public class RopeSystem : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("체크하면 로프 시스템을 완전히 비활성화합니다. (나홀로 등반 테스트용)")]
    public bool disableRopeForTesting = false;

    [Header("Rope Settings")]
    public Vector3 localTieOffset = new Vector3(0, -0.5f, 0);
    public float maxRopeLength = 3.0f;

    [Header("Asset Reference")]
    public Rope assetRope;

    private Transform myBodyTransform;
    private Transform partnerTransform;

    /// <summary>
    /// GamePlayerSpawner에서 두 플레이어가 모두 접속한 뒤 호출합니다.
    /// </summary>
    public void SetPartners(Transform myBody, Transform partner)
    {
        myBodyTransform = myBody;
        partnerTransform = partner;
        AttachRopeToAsset();
    }

    private void AttachRopeToAsset()
    {
        if (assetRope == null || myBodyTransform == null || partnerTransform == null) return;

        assetRope.SetStartPoint(myBodyTransform, true);
        assetRope.SetEndPoint(partnerTransform, true);

        Debug.Log("[RopeSystem] 로프 연결 완료.");
    }

    public void LimitMovement(ref Vector3 proposedDeltaWorld)
    {
        if (disableRopeForTesting) return;
        if (partnerTransform == null || myBodyTransform == null) return;

        Vector3 myTiePoint = myBodyTransform.TransformPoint(localTieOffset);
        Vector3 partnerPos = partnerTransform.position;

        Vector3 predictedPos = myTiePoint - proposedDeltaWorld;

        float currentDistance = Vector3.Distance(myTiePoint, partnerPos);
        float predictedDistance = Vector3.Distance(predictedPos, partnerPos);

        if (predictedDistance > maxRopeLength && predictedDistance > currentDistance)
        {
            if (currentDistance >= maxRopeLength)
            {
                proposedDeltaWorld = Vector3.zero;
                return;
            }

            Vector3 V = -proposedDeltaWorld;
            Vector3 L = myTiePoint - partnerPos;

            float a = Vector3.Dot(V, V);
            if (a < 0.0001f) return;

            float b = 2f * Vector3.Dot(V, L);
            float c = Vector3.Dot(L, L) - (maxRopeLength * maxRopeLength);

            float discriminant = (b * b) - (4f * a * c);

            if (discriminant >= 0)
            {
                float t = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
                proposedDeltaWorld *= Mathf.Clamp01(t);
            }
            else
            {
                proposedDeltaWorld = Vector3.zero;
            }
        }
    }
}
