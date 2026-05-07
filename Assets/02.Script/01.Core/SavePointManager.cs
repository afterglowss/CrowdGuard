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

    /// <summary>
    /// 텐트 방문 시에만 갱신되는 전용 세이브 포인트.
    /// 앵커가 박힌 벽이 부서졌을 때 이 위치로 리스폰합니다.
    /// </summary>
    private Vector3 _tentSavePosition;
    private bool _hasTentSave = false;

    /// <summary>앵커 벽 파괴 시 세이브 포인트가 텐트로 복원될 때 발행됩니다.</summary>
    public static event Action<Vector3> OnSavePointRevertedToTent;

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
        _tentSavePosition = defaultSpawnPosition;
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
    /// 텐트 세이브 포인트(wallNormal == zero)는 오프셋 없이 그대로 반환합니다.
    /// </summary>
    private Vector3 CalcRespawnBase()
    {
        // 텐트 세이브 포인트: wallNormal이 zero이므로 오프셋을 적용하지 않는다.
        // (텐트 퇴장 위치는 이미 적절한 월드 좌표이므로 추가 보정 불필요)
        if (_lastWallNormal.sqrMagnitude < 0.01f)
            return lastSafePosition;

        Vector3 outward = _lastWallNormal; // 이미 Normalized 보장됨
        Vector3 pos     = lastSafePosition + outward * wallNormalOffset;
        pos.y          -= chestHeightOffset;
        return pos;
    }

    /// <summary>
    /// RPC 핸들러 내부에서 호출용. 추가 RPC를 발송하지 않고 로컬 상태만
    /// 텐트 세이브 포인트로 복원합니다.
    /// _lastWallNormal을 zero로 초기화해 CalcRespawnBase가 오프셋을 적용하지
    /// 않도록 합니다.
    /// </summary>
    public void LocalRevertToTentSavePoint()
    {
        // 텐트 방문 기록이 없으면 기본 스폰 위치로 폴백한다.
        Vector3 target = _hasTentSave ? _tentSavePosition : defaultSpawnPosition;

        lastSafePosition = target;
        _lastWallNormal  = Vector3.zero; // 텐트/기본 위치 모두 벽 법선 없음
        OnSavePointChanged?.Invoke(lastSafePosition);
        if (_hasTentSave)
            OnSavePointRevertedToTent?.Invoke(_tentSavePosition);
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

    /// <summary>
    /// 텐트 퇴장 시 호출. lastSafePosition과 텐트 전용 _tentSavePosition을 동시에 갱신합니다.
    /// </summary>
    public void ForceSetSavePoint(Vector3 position)
    {
        RPC_SetTentSavePoint(position);
        Debug.Log($"[SavePointManager] 텐트 세이브 포인트 갱신! ({position})");
    }

    /// <summary>
    /// 텐트 위치를 모든 클라이언트에 동기화합니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetTentSavePoint(Vector3 position)
    {
        lastSafePosition   = position;
        _tentSavePosition  = position;
        _hasTentSave       = true;
        OnSavePointChanged?.Invoke(lastSafePosition);
    }

    /// <summary>
    /// 앵커가 박힌 벽이 파괴됐을 때 호출.
    /// 즉각 리스폰이 아니라, lastSafePosition을 텐트 위치로 되돌려
    /// 다음 사망 시 텐트에서 부활하도록 합니다.
    /// </summary>
    public void RevertToTentSavePoint()
    {
        if (!_hasTentSave) return;
        Debug.Log($"[SavePointManager] 앵커 벽 파괴 — 세이브 포인트를 텐트로 복원: {_tentSavePosition}");
        RPC_SetLastSafePosition(_tentSavePosition);
        OnSavePointRevertedToTent?.Invoke(_tentSavePosition);
    }
}
