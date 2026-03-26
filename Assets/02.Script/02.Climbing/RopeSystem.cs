using System;
using UnityEngine;
using Fusion;

[RequireComponent(typeof(LineRenderer))]
public class RopeSystem : MonoBehaviour
{
    [Header("Rope Settings")]
    [Tooltip("내 등반자 기준 트랜스폼 (보통 Main Camera인 HMD를 넣습니다)")]
    public Transform myBodyTransform;
    [Tooltip("파트너 플레이어(임시 더미 큐브) 트랜스폼")]
    public Transform partnerTransform;
    [Tooltip("기준(HMD)에서 줄이 묶여있는 오프셋 (머리 기준 반 미터 아래: Y=-0.5)")]
    public Vector3 localTieOffset = new Vector3(0, -0.5f, 0); 
    [Tooltip("가상 로프의 최대 도달 허용 거리")]
    public float maxRopeLength = 3.0f;

    [Header("Visuals (Curve)")]
    [Tooltip("부드러운 곡선을 그리기 위한 점의 개수")]
    public int linePoints = 15;
    [Tooltip("거리가 가까울 때 밧줄이 아래로 축 쳐지는 중력 강도")]
    public float sagMultiplier = 1.0f; 
    public Color normalColor = Color.white;
    public Color tensionColor = Color.red;
    private LineRenderer lineRenderer;

    public event Action OnRopeTensionMax;

    private Transform dummyTransform;
    private float searchTimer = 0f;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = linePoints; 
    }

    private void Start()
    {
        // 인스펙터에 수동으로 넣어둔 객체(예: 혼자 테스트용 큐브)를 더미로 기억합니다.
        dummyTransform = partnerTransform;
    }

    private void FindNetworkPartner()
    {
        // 게임씬 내에 네트워크 팀원이 만든 PlayerModel 아바타들을 싹 찾습니다.
        PlayerModel[] allPlayers = FindObjectsOfType<PlayerModel>();
        
        foreach (var p in allPlayers)
        {
            // 네트워크 객체 중 '내 조작 권한이 없는(HasInputAuthority == false)' 객체가 바로 상대방입니다!
            if (p.Object != null && p.Object.IsValid && !p.HasInputAuthority)
            {
                // 상대 플레이어의 뼈대(아바타의 최상단)를 내 로프 파트너로 강제 교체합니다.
                partnerTransform = p.transform;
                Debug.Log($"[RopeSystem] 파트너({p.gameObject.name}) 접속 확인! 더미 큐브를 버리고 실제 유저와 밧줄을 묶습니다!");
                return;
            }
        }
    }

    private void Update()
    {
        // 파트너가 아직 안 들어와서 더미에 묶여있다면, 프레임 과부하를 막기 위해 1초에 한 번씩만 파트너를 찾습니다.
        if (partnerTransform == null || partnerTransform == dummyTransform)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= 1.0f)
            {
                searchTimer = 0f;
                FindNetworkPartner();
            }
        }

        if (partnerTransform == null || myBodyTransform == null) return;

        Vector3 startPos = myBodyTransform.TransformPoint(localTieOffset);
        Vector3 endPos = partnerTransform.position;

        float distance = Vector3.Distance(startPos, endPos);
        float tensionRatio = Mathf.Clamp01(distance / maxRopeLength);

        // 시각적 텐션 그라데이션 적용 (거리가 멀수록 붉게)
        Color currentColor = Color.Lerp(normalColor, tensionColor, Mathf.Pow(tensionRatio, 4));
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;

        // 거리가 팽팽해지면 sag(늘어짐)가 0이 되고, 가까우면 둥글게 쳐집니다 (가짜 곡선 수학)
        float currentSag = sagMultiplier * (1f - tensionRatio);
        Vector3 midPos = (startPos + endPos) / 2f;
        midPos.y -= currentSag; // 중력

        for (int i = 0; i < linePoints; i++)
        {
            float t = i / (float)(linePoints - 1);
            Vector3 pointOnCurve = CalculateQuadraticBezierPoint(t, startPos, midPos, endPos);
            lineRenderer.SetPosition(i, pointOnCurve);
        }
    }

    // 간단한 베지어 곡선(이차) 공식을 이용해 곡선을 그립니다.
    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        Vector3 p = uu * p0; 
        p += 2 * u * t * p1; 
        p += tt * p2;        
        return p;
    }

    public void LimitMovement(ref Vector3 proposedDeltaWorld)
    {
        if (partnerTransform == null || myBodyTransform == null) return;

        Vector3 myTiePoint = myBodyTransform.TransformPoint(localTieOffset);
        Vector3 partnerPos = partnerTransform.position;

        Vector3 predictedPos = myTiePoint - proposedDeltaWorld;
        float predictedDistance = Vector3.Distance(predictedPos, partnerPos);

        if (predictedDistance > maxRopeLength)
        {
            Vector3 directionFromPartner = (predictedPos - partnerPos).normalized;
            Vector3 clampedPos = partnerPos + (directionFromPartner * maxRopeLength);
            proposedDeltaWorld = myTiePoint - clampedPos;

            OnRopeTensionMax?.Invoke();
        }
    }
}
