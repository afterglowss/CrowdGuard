using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 게임 오버 이벤트를 모든 클라이언트에 전파합니다.
/// HazardManager와 동일한 Fusion RPC 패턴을 사용합니다.
/// </summary>
public class GameOverManager : NetworkBehaviour
{
    public static GameOverManager Instance { get; private set; }

    /// <summary>
    /// 모든 클라이언트에서 게임 오버 발생 시 호출됩니다.
    /// GameOverUI 등 로컬 UI 시스템이 이 이벤트를 구독합니다.
    /// </summary>
    public static event Action OnGameOver;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 오브젝트 상호작용 시 호출. 모든 클라이언트에 게임 오버를 알립니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_NotifyGameOver()
    {
        Debug.Log("[GameOverManager] 게임 오버!");
        OnGameOver?.Invoke();
    }
}
