using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class SavePointManager : NetworkBehaviour
{
    public static SavePointManager Instance { get; private set; }

    /// <summary>
    /// 세이브 포인트가 갱신될 때마다 발행됩니다. (앵커 체결 / 텐트 퇴장)
    /// LeaderSafetyRope 등 시각화 컴포넌트가 구독합니다.
    ///
    /// ※ 앵커 체결은 IceAnchorController RPC를 통해 전 클라이언트에 전파되므로
    ///    양쪽에서 자동 발행됩니다.
    ///    텐트 퇴장은 ForceSetSavePoint()를 호출하는 쪽에서만 발행되므로,
    ///    팔로워에게도 보여야 한다면 호출부에서 별도 RPC 처리가 필요합니다.
    /// </summary>
    public static event Action<Vector3> OnSavePointChanged;

    [Tooltip("게임 시작 기본 위치 (앵커를 한 번도 안 박고 떨어졌을 때 부활할 곳)")]
    public Vector3 defaultSpawnPosition;

    [Tooltip("앵커에서 벽 법선(바깥 방향)으로 플레이어를 얼마나 띄울지 (m). 0.2~0.5 권장")]
    public float wallNormalOffset = 0.3f;

    [Tooltip("XR Rig는 발 기준이므로 앵커가 가슴 높이에 오려면 아래로 내려야 합니다. 플레이어 가슴 높이(m). 1.2~1.4 권장")]
    public float chestHeightOffset = 1.2f;

    [Tooltip("2인 플레이 시 두 플레이어 사이 가로 간격 (m)")]
    public float twoPlayerSpacing = 0.6f;

    private Vector3 lastSafePosition;

    /// <summary>체결 당시의 벽 법선. 리스폰 오프셋 방향 계산에 사용.</summary>
    private Vector3 _lastWallNormal = Vector3.back; // 기본값: -Z (정면 벽 가정)

    /// <summary>가장 최근 세이브 포인트 위치 (리스폰 오프셋 미포함 순수 좌표).</summary>
    public Vector3 LastSavePosition => lastSafePosition;

    // 체결된 앵커 위치 전체 목록 (AnchorSafeZone에서 참조)
    private readonly List<Vector3> _securedAnchorPositions = new List<Vector3>();
    public IReadOnlyList<Vector3> SecuredAnchorPositions => _securedAnchorPositions;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        lastSafePosition = defaultSpawnPosition;
    }

    private void OnEnable()
    {
        CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorController.OnAnchorSecuredGlobal += HandleAnchorSecured;
    }

    private void OnDisable()
    {
        CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorController.OnAnchorSecuredGlobal -= HandleAnchorSecured;
    }

    private void HandleAnchorSecured(CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorModel model, Vector3 wallNormal)
    {
        RPC_SetSavePoint(model.transform.position, wallNormal);
        _securedAnchorPositions.Add(model.transform.position);
        Debug.Log($"[SavePointManager] 세이브 포인트 갱신! ({model.transform.position}) / wallNormal={wallNormal} / 전체 앵커 수: {_securedAnchorPositions.Count}");
    }

    /// <summary>
    /// 앵커 체결 시 위치 + 벽 법선을 동시에 동기화합니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetSavePoint(Vector3 position, Vector3 wallNormal)
    {
        lastSafePosition = position;
        _lastWallNormal  = wallNormal.sqrMagnitude > 0.001f ? wallNormal.normalized : Vector3.back;
        OnSavePointChanged?.Invoke(lastSafePosition);
    }

    /// <summary>
    /// 텐트 퇴장 등 벽 법선이 필요 없는 경우에만 사용합니다.
    /// wallNormal은 기존 값을 유지합니다.
    /// </summary>
    [Rpc(RpcSources.All,RpcTargets.All)]
    public void RPC_SetLastSafePosition(Vector3 position)
    {
        lastSafePosition = position;
        OnSavePointChanged?.Invoke(lastSafePosition);
    }

    /// <summary>
    /// 벽 법선 방향으로 wallNormalOffset 만큼 띄우고,
    /// XR Rig 발 기준 Y를 앵커보다 chestHeightOffset 만큼 내려 가슴 높이를 맞춥니다.
    /// </summary>
    private Vector3 CalcRespawnBase()
    {
        Vector3 outward = _lastWallNormal; // 이미 Normalized 보장됨
        Vector3 pos     = lastSafePosition + outward * wallNormalOffset;
        pos.y          -= chestHeightOffset;
        return pos;
    }

    /// <summary>
    /// 1번 플레이어(리더) 리스폰 위치
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        return CalcRespawnBase() + new Vector3(-twoPlayerSpacing * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// 2번 플레이어(서포터) 리스폰 위치
    /// </summary>
    public Vector3 GetRespawnPositionP2()
    {
        return CalcRespawnBase() + new Vector3(twoPlayerSpacing * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// 플레이어 위치가 어떤 앵커로부터 safeRadius 이내에 있는지 체크
    /// </summary>
    public bool IsNearAnyAnchor(Vector3 playerPosition, float safeRadius)
    {
        foreach (var anchorPos in _securedAnchorPositions)
        {
            if (Vector3.Distance(playerPosition, anchorPos) <= safeRadius)
                return true;
        }
        return false;
    }

    public void ForceSetSavePoint(Vector3 position)
    {
        RPC_SetLastSafePosition(position);
        Debug.Log($"[SavePointManager] 세이브 포인트 강제 갱신! ({lastSafePosition})");
    }
}
