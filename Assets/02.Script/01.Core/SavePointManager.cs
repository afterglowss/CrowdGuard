using System;
using System.Collections.Generic;
using UnityEngine;

public class SavePointManager : MonoBehaviour
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

    [Tooltip("리스폰 위치를 앵커로부터 얼마나 띄울지 (벽에서 멀어지는 방향). 에디터에서 조절하세요.")]
    public Vector3 respawnOffset = new Vector3(0f, 0f, -1.5f);

    [Tooltip("2인 플레이 시 두 플레이어 사이 가로 간격 (m)")]
    public float twoPlayerSpacing = 0.6f;

    private Vector3 lastSafePosition;

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

    private void HandleAnchorSecured(CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorModel model)
    {
        lastSafePosition = model.transform.position;
        _securedAnchorPositions.Add(lastSafePosition);
        Debug.Log($"[SavePointManager] 세이브 포인트 갱신! ({lastSafePosition}) / 전체 앵커 수: {_securedAnchorPositions.Count}");
        OnSavePointChanged?.Invoke(lastSafePosition);
    }

    /// <summary>
    /// 1번 플레이어 리스폰 위치 (앵커 기준 오프셋 + 왼쪽으로 spacing/2)
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        return lastSafePosition + respawnOffset + new Vector3(-twoPlayerSpacing * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// 2번 플레이어 리스폰 위치 (앵커 기준 오프셋 + 오른쪽으로 spacing/2)
    /// </summary>
    public Vector3 GetRespawnPositionP2()
    {
        return lastSafePosition + respawnOffset + new Vector3(twoPlayerSpacing * 0.5f, 0f, 0f);
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
        lastSafePosition = position;
        Debug.Log($"[SavePointManager] 세이브 포인트 강제 갱신! ({lastSafePosition})");
        OnSavePointChanged?.Invoke(lastSafePosition);
    }
}
