using Fusion;
using UnityEngine;

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

        if (_spawnedPlayerCount < 2) return;
        if (_roleManagerDestroyRequested) return;
        if (RoleManager.Instance == null) return;

        _roleManagerDestroyRequested = true;
        RoleManager.Instance.RPC_DestroyAfterRoleDistributed();
        Debug.Log("[GameManager] 양쪽 플레이어 스폰 완료 → RoleManager 파괴 요청");
    }
}
