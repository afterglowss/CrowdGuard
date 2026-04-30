using UnityEngine;
using GogoGaga.OptimizedRopesAndCables;

/// <summary>
/// 리더 플레이어와 최신 세이브 포인트(앵커 / 텐트)를 연결하는 안전 밧줄.
///
/// [설계 원칙]
/// - 물리 제한 없음. 순수 비주얼 로프.
/// - 양쪽 클라이언트가 로컬에서 독립 렌더링.
///   (로프 오브젝트에 NetworkObject를 붙이지 않아 네트워크 부하 없음)
/// - SavePointManager.OnSavePointChanged 이벤트를 구독해
///   앵커 체결 / 텐트 퇴장마다 자동 갱신.
///
/// [씬 세팅 방법]
/// 1. 리더 플레이어 프리팹에 이 컴포넌트를 추가합니다.
/// 2. GogoGaga Rope 컴포넌트가 붙은 자식 오브젝트를 assetRope 슬롯에 연결합니다.
/// 3. GamePlayerModel.Spawned()에서 SetLeaderBody()를 호출해 몸통 트랜스폼을 주입합니다.
///    (리더가 아닌 클라이언트에서는 SetLeaderBody() 호출만으로 팔로워 시점 렌더링도 가능)
///
/// [네트워크 주의]
/// - 앵커 체결: IceAnchorController RPC가 전 클라이언트에 전파 → 자동 동기화 됨.
/// - 텐트 퇴장: TentInteriorController에서 ForceSetSavePoint()를 호출할 때,
///   팔로워 클라이언트에서도 같은 위치가 저장되어 있어야 합니다.
///   현재 TentInteriorController가 로컬만 호출한다면, 호출부에서 추가 RPC가 필요합니다.
/// </summary>
public class LeaderSafetyRope : MonoBehaviour
{
    [Header("Rope Asset")]
    [Tooltip("GogoGaga Rope 컴포넌트. 자식 오브젝트에 붙여서 인스펙터에서 연결하세요.")]
    public Rope assetRope;

    [Header("Rope Settings")]
    [Tooltip("플레이어 측 로프 연결점 로컬 오프셋. Y=-0.5 정도면 가슴 높이.")]
    public Vector3 playerAnchorLocalOffset = new Vector3(0f, -0.5f, 0f);

    [Tooltip("세이브 포인트 갱신 시 ropeLength = 실제거리 + slackAmount. 클수록 더 처집니다.")]
    public float slackAmount = 1.5f;

    [Tooltip("체크하면 세이프티 로프를 완전히 비활성화합니다. (1인 테스트용)")]
    public bool disableForTesting = false;

    // ── 내부 앵커 오브젝트 ──────────────────────────────────────────
    // 플레이어 측: 리더 몸통 트랜스폼의 자식 → 몸통을 자동으로 따라감
    private Transform _playerAnchor;

    // 세이브포인트 측: 씬 루트 독립 오브젝트 → 세이브 포인트 갱신 시 이동
    private Transform _savePointAnchor;

    // 현재 연결된 리더 몸통 트랜스폼 (Camera.main 또는 네트워크 동기화된 파트너 트랜스폼)
    private Transform _leaderBodyTransform;

    // ── 초기화 API ──────────────────────────────────────────────────

    /// <summary>
    /// 리더 몸통(카메라 또는 XR Head) 트랜스폼을 주입합니다.
    /// GamePlayerModel.Spawned()에서 호출하세요.
    ///
    /// - 로컬 리더 클라이언트: Camera.main.transform 또는 xrRigPivot 자식 Head 트랜스폼
    /// - 팔로워 클라이언트:    네트워크 동기화된 리더의 Head 트랜스폼
    /// </summary>
    public void SetLeaderBody(Transform leaderBodyTransform)
    {
        _leaderBodyTransform = leaderBodyTransform;

        // 플레이어 앵커 재생성 (SetPartners가 여러 번 호출될 때 안전)
        if (_playerAnchor != null) Destroy(_playerAnchor.gameObject);

        var playerAnchorGo = new GameObject("_SafetyRope_PlayerAnchor");
        playerAnchorGo.transform.SetParent(_leaderBodyTransform);
        playerAnchorGo.transform.localPosition = playerAnchorLocalOffset;
        playerAnchorGo.transform.localRotation = Quaternion.identity;
        _playerAnchor = playerAnchorGo.transform;

        // 세이브포인트 앵커가 없으면 생성 (씬에 하나만)
        if (_savePointAnchor == null)
        {
            var spGo = new GameObject("_SafetyRope_SavePointAnchor");
            // 씬 루트에 독립 배치 — 어느 오브젝트에도 종속되지 않음
            _savePointAnchor = spGo.transform;
        }

        // 현재 세이브 포인트 위치로 초기 배치
        if (SavePointManager.Instance != null)
            _savePointAnchor.position = SavePointManager.Instance.LastSavePosition;

        AttachRopeToAsset();
        RecalculateRopeLength();
    }

    // ── Unity 생명주기 ──────────────────────────────────────────────

    private void OnEnable()
    {
        SavePointManager.OnSavePointChanged += OnSavePointUpdated;
    }

    private void OnDisable()
    {
        SavePointManager.OnSavePointChanged -= OnSavePointUpdated;
    }

    private void OnDestroy()
    {
        // 씬 전환 / 오브젝트 파괴 시 생성한 앵커 오브젝트도 정리
        if (_playerAnchor    != null) Destroy(_playerAnchor.gameObject);
        if (_savePointAnchor != null) Destroy(_savePointAnchor.gameObject);
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────

    private void OnSavePointUpdated(Vector3 newSavePointPos)
    {
        if (disableForTesting) return;
        if (_savePointAnchor == null || _playerAnchor == null) return;

        // 1. 세이브포인트 앵커를 새 위치로 이동
        _savePointAnchor.position = newSavePointPos;

        // 2. ropeLength 재계산: 현재거리 + 여유분
        //    → "방금 앵커를 박은 위치 근처" 기준으로 처짐이 결정됨
        //    → 플레이어가 위로 올라갈수록 자연스럽게 팽팽해짐
        RecalculateRopeLength();

        Debug.Log($"[LeaderSafetyRope] 세이프티 로프 갱신. 세이브포인트={newSavePointPos}, ropeLength={assetRope?.ropeLength:F2}");
    }

    // ── 가시성 제어 ────────────────────────────────────────────────

    /// <summary>
    /// 로프 메시의 표시 여부를 전환합니다.
    /// 텐트 내부에서는 숨기고, 퇴장 시 다시 표시합니다.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (assetRope == null) return;
        var mr = assetRope.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = visible;
    }

    // ── 내부 유틸 ──────────────────────────────────────────────────

    private void AttachRopeToAsset()
    {
        if (assetRope == null || _playerAnchor == null || _savePointAnchor == null) return;

        assetRope.SetStartPoint(_playerAnchor,    instantAssign: true);
        assetRope.SetEndPoint  (_savePointAnchor, instantAssign: true);
    }

    private void RecalculateRopeLength()
    {
        if (assetRope == null || _playerAnchor == null || _savePointAnchor == null) return;

        float dist = Vector3.Distance(_playerAnchor.position, _savePointAnchor.position);
        assetRope.ropeLength = dist + slackAmount;
        assetRope.RecalculateRope();
    }

    // ── Gizmo ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (_playerAnchor == null || _savePointAnchor == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f); // 노란색
        Gizmos.DrawLine(_playerAnchor.position, _savePointAnchor.position);

        Gizmos.DrawSphere(_playerAnchor.position,    0.05f);
        Gizmos.DrawSphere(_savePointAnchor.position, 0.08f);

#if UNITY_EDITOR
        float dist = Vector3.Distance(_playerAnchor.position, _savePointAnchor.position);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(
            (_playerAnchor.position + _savePointAnchor.position) * 0.5f + Vector3.right * 0.1f,
            $"SafetyRope  {dist:F2}m  (rope={assetRope?.ropeLength:F2}m)");
#endif
    }
}
