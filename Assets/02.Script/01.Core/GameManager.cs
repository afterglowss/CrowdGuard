using Fusion;
using UnityEngine;
using CrowdGuard.Climbing.Tools.Common;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private int _spawnedPlayerCount = 0;
    private bool _roleManagerDestroyRequested = false;

    private void Awake()
    {
        if (!Instance) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    public void GameStart()
    {
        // TODO : GameManager NetworkBehaviour로 변경 및 RPC 추가
        Debug.Log("GameStart");
    }

    /// <summary>
    /// GamePlayerModel.Spawned()에서 플레이어 하나가 스폰될 때마다 호출.
    /// 양쪽 모두 스폰 완료 시 RoleManager를 파괴합니다.
    /// </summary>
    public void OnPlayerModelSpawned()
    {
        _spawnedPlayerCount++;

        // None(관전자)을 제외한 실제 플레이어 수로 비교
        // Roles에 관전자(None)가 포함돼 있어도 게임 플레이어는 항상 2명
        int actualPlayerCount = 0;
        foreach (var kv in RoleManager.Instance.Roles)
        {
            if (kv.Value != PlayerRole.None) actualPlayerCount++;
        }

        if (_spawnedPlayerCount < actualPlayerCount) return;
        if (_roleManagerDestroyRequested) return;
        if (RoleManager.Instance == null) return;

        _roleManagerDestroyRequested = true;
        Destroy(RoleManager.Instance.gameObject);
        //RoleManager.Instance.RPC_DestroyAfterRoleDistributed();
        Debug.Log("[GameManager] 양쪽 플레이어 스폰 완료 → RoleManager 파괴 요청");
    }

    public void GameEnd()
    {
        Debug.Log("GameEnd");
        DataManager.Instance?.timer.Pause();
    }
}
