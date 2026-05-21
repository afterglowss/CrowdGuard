using UnityEngine;
using GogoGaga.OptimizedRopesAndCables;

public class RopeSystem : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("체크하면 로프 시스템을 완전히 비활성화합니다. (나홀로 등반 테스트용)")]
    public bool disableRopeForTesting = false;

    [Header("Rope Settings")]
    [Tooltip("body 트랜스폼 기준 로프 묶음 위치 로컬 오프셋. body가 발 기준이면 Y=1.2~1.4 정도가 가슴 높이.")]
    public Vector3 localTieOffset = new Vector3(0, 1.2f, 0);
    public float maxRopeLength = 3.0f;

    [Header("Asset Reference")]
    public Rope assetRope;

    [Header("Haptics")]
    [Tooltip("로프가 팽팽해질 때 재생할 햅틱 프로파일 (루핑 재생 설정 권장)")]
    [SerializeField] private CrowdGuard.XR.Haptics.HapticProfile _tensionHapticProfile;
    [Tooltip("햅틱 재생이 트리거될 장력 비율 임계값 (0.0 ~ 1.0)")]
    [SerializeField][Range(0f, 1f)] private float _tensionThreshold = 0.85f;

    private Transform myBodyTransform;
    private Transform partnerTransform;

    // localTieOffset이 적용된 실제 로프 묶음 위치 (시각 + 물리 계산 공용)
    private Transform _myAnchor;
    private Transform _partnerAnchor;

    // LimitMovement용 실시간 기준점 — xrRigPivot의 자식으로 ObjectTracker 지연 없음
    private Transform _myRigPivot;
    private Transform _myPhysicsAnchor;

    // Haptics 런타임 제어 상태
    private CrowdGuard.XR.Haptics.IHapticProvider[] _hapticProviders;
    private bool _isTensionHapticActive = false;

    /// <summary>
    /// PlayerManager에서 두 플레이어가 모두 접속한 뒤 호출합니다.
    /// myBody / partner 모두 카메라(머리) 트랜스폼이므로
    /// localTieOffset으로 가슴 위치를 잡아 자식 앵커를 생성합니다.
    /// myRigPivot은 로컬 플레이어의 xrRigPivot으로, LimitMovement에서
    /// ObjectTracker 지연 없이 실시간 위치를 계산하는 데 사용합니다.
    /// </summary>
    public void SetPartners(Transform myBody, Transform partner, Transform myRigPivot)
    {
        myBodyTransform = myBody;
        partnerTransform = partner;
        _myRigPivot = myRigPivot;

        // 기존 앵커가 있으면 제거 후 재생성 (재연결 안전 처리)
        if (_myAnchor != null) Destroy(_myAnchor.gameObject);
        if (_partnerAnchor != null) Destroy(_partnerAnchor.gameObject);
        if (_myPhysicsAnchor != null) Destroy(_myPhysicsAnchor.gameObject);

        _myAnchor = CreateAnchor("_RopeAnchor_Me", myBodyTransform, localTieOffset);
        _partnerAnchor = CreateAnchor("_RopeAnchor_Partner", partnerTransform, localTieOffset);

        // xrRigPivot의 자식으로 물리 앵커 생성.
        // ObjectTracker lerp 지연 없이 locomotion 이동을 즉시 반영합니다.
        // body가 발 기준(xrRigPivot과 동일 레벨)이므로 localTieOffset을 그대로 사용합니다.
        if (_myRigPivot != null)
            _myPhysicsAnchor = CreateAnchor("_RopePhysicsAnchor_Me", _myRigPivot, localTieOffset);

        // 로컬 플레이어의 최상위 조상(보통 XR Origin)을 찾아 그 하위 전체에서 햅틱 프로바이더들(Left/Right 양손)을 100% 안전하게 탐색
        Transform playerRoot = myBodyTransform;
        while (playerRoot.parent != null)
        {
            playerRoot = playerRoot.parent;
        }
        _hapticProviders = playerRoot.GetComponentsInChildren<CrowdGuard.XR.Haptics.IHapticProvider>(true);

        _isTensionHapticActive = false;

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

    private void Update()
    {
        UpdateTensionHaptics();
    }

    private void UpdateTensionHaptics()
    {
        if (_hapticProviders == null || _hapticProviders.Length == 0 || _tensionHapticProfile == null) return;

        float ratio = GetStretchRatio();

        if (ratio >= _tensionThreshold && !_isTensionHapticActive)
        {
            Debug.Log($"[RopeSystem] 장력 초과 ({ratio:F2} >= {_tensionThreshold:F2}). 장력 햅틱 루프 시작.");
            foreach (var provider in _hapticProviders)
            {
                provider.PlayLoopingHaptic(_tensionHapticProfile);
            }
            _isTensionHapticActive = true;
        }
        else if (ratio < _tensionThreshold && _isTensionHapticActive)
        {
            Debug.Log($"[RopeSystem] 장력 완화 ({ratio:F2} < {_tensionThreshold:F2}). 장력 햅틱 정지.");
            foreach (var provider in _hapticProviders)
            {
                provider.StopHaptic();
            }
            _isTensionHapticActive = false;
        }
    }

    public void LimitMovement(ref Vector3 proposedDeltaWorld)
    {
        if (disableRopeForTesting) return;
        if (_partnerAnchor == null) return;

        // xrRigPivot 자식 물리 앵커로 실시간 위치 계산 (ObjectTracker lerp 지연 없음).
        // _myPhysicsAnchor가 없으면 ObjectTracker 기반 _myAnchor로 폴백.
        Transform activeTiePoint = _myPhysicsAnchor != null ? _myPhysicsAnchor : _myAnchor;
        if (activeTiePoint == null) return;

        Vector3 myTiePoint = activeTiePoint.position;

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
