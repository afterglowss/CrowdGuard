using UnityEngine;
using GogoGaga.OptimizedRopesAndCables;

public class RopeSystem : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("체크하면 로프 시스템을 완전히 비활성화합니다. (나홀로 등반 테스트용)")]
    public bool disableRopeForTesting = false;

    [Header("Rope Settings")]
    [Tooltip("카메라(머리) 기준으로 로프가 묶이는 위치 오프셋. Y=-0.5 정도면 가슴 높이.")]
    public Vector3 localTieOffset = new Vector3(0, -0.5f, 0);
    public float maxRopeLength = 3.0f;

    [Header("Asset Reference")]
    public Rope assetRope;

    private Transform myBodyTransform;
    private Transform partnerTransform;

    // localTieOffset이 적용된 실제 로프 묶음 위치 (시각 + 물리 계산 공용)
    private Transform _myAnchor;
    private Transform _partnerAnchor;

    /// <summary>
    /// PlayerManager에서 두 플레이어가 모두 접속한 뒤 호출합니다.
    /// myBody / partner 모두 카메라(머리) 트랜스폼이므로
    /// localTieOffset으로 가슴 위치를 잡아 자식 앵커를 생성합니다.
    /// </summary>
    public void SetPartners(Transform myBody, Transform partner)
    {
        myBodyTransform = myBody;
        partnerTransform = partner;

        // 기존 앵커가 있으면 제거 후 재생성 (재연결 안전 처리)
        if (_myAnchor != null) Destroy(_myAnchor.gameObject);
        if (_partnerAnchor != null) Destroy(_partnerAnchor.gameObject);

        _myAnchor = CreateAnchor("_RopeAnchor_Me", myBodyTransform, localTieOffset);
        _partnerAnchor = CreateAnchor("_RopeAnchor_Partner", partnerTransform, localTieOffset);

        AttachRopeToAsset();
    }

    /// <summary>
    /// 부모 트랜스폼의 자식으로 오프셋 앵커 오브젝트를 생성합니다.
    /// SetParent로 부모를 따라가므로 별도 Update 불필요.
    /// </summary>
    private Transform CreateAnchor(string name, Transform parent, Vector3 localOffset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }

    private void AttachRopeToAsset()
    {
        if (assetRope == null || _myAnchor == null || _partnerAnchor == null) return;

        // 머리(카메라) 대신 오프셋 앵커를 로프 양 끝단에 연결
        assetRope.SetStartPoint(_myAnchor, true);
        assetRope.SetEndPoint(_partnerAnchor, true);

        Debug.Log($"[RopeSystem] 로프 연결 완료. 오프셋={localTieOffset}");
    }

    /// <summary>
    /// 현재 두 플레이어 앵커 간 거리를 maxRopeLength로 나눈 팽팽함 비율.
    /// 0.0 = 완전히 붙어있음 / 1.0 = 로프가 완전히 팽팽함.
    /// AvalancheRiskManager 등 외부에서 분리도 지표로 사용합니다.
    /// </summary>
    public float GetStretchRatio()
    {
        if (_myAnchor == null || _partnerAnchor == null) return 0f;
        float current = Vector3.Distance(_myAnchor.position, _partnerAnchor.position);
        return Mathf.Clamp01(current / maxRopeLength);
    }

    public void LimitMovement(ref Vector3 proposedDeltaWorld)
    {
        if (disableRopeForTesting) return;
        if (_myAnchor == null || _partnerAnchor == null) return;

        // 앵커가 이미 오프셋 적용된 위치이므로 TransformPoint 불필요
        Vector3 myTiePoint = _myAnchor.position;
        Vector3 partnerPos = _partnerAnchor.position;

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
