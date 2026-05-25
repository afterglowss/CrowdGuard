using System;
using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.Common;
using Fusion;
using UnityEngine;

public class RoleManager : NetworkBehaviour, IStateAuthorityChanged
{
    public static RoleManager Instance{ get; private set;} 

    [Networked,OnChangedRender(nameof(RoleChanged))] public NetworkDictionary<PlayerRef,PlayerRole> Roles { get; }

    public event Action<bool> OnRoleAccepted;
    
    // 싱글턴 설정
    public override void Spawned()
    {
        Instance = this;
        RoleChanged();
        base.Spawned();
        Runner.MakeDontDestroyOnLoad(gameObject);
        
    }

    // 싱글턴 제거
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Instance = null;
        base.Despawned(runner, hasState);
    }

    /// <summary>
    /// 역할 세팅
    /// </summary>
    /// <param name="player"></param>
    /// <param name="role"></param>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerRole(PlayerRef player, PlayerRole role)
    {
        foreach (var item in Roles)
        {
            if (item.Value == role)
            {
                Roles.Remove(item.Key);
            }
        }
        Roles.Set(player,role);
    }
    

    private bool _despawnRequested = false;

    /// <summary>
    /// 게임 씬에서 역할 배분이 끝난 뒤 GameManager에서 호출.
    /// StateAuthority가 Despawn → 모든 클라이언트에서 동시 파괴.
    /// 양쪽 클라이언트 모두 호출해도 한 번만 실행됩니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DestroyAfterRoleDistributed()
    {
        if (_despawnRequested) return;
        _despawnRequested = true;
        Runner.Despawn(Object);
    }

    /// <summary>
    /// 역할 변화 시 게임 시작 가능한지 판별하기
    /// </summary>
    private void RoleChanged()
    {
        var role = PlayerRole.None;

        foreach (var item in Roles)
        {
            if (role == PlayerRole.None)
            {
                role = item.Value;
            }
            else
            {
                // 두 번째 플레이어의 역할이 첫 번째와 다르면 성공(true)
                if (role == item.Value) continue;
                OnRoleAccepted?.Invoke(true);
                return;
            }
        }
        OnRoleAccepted?.Invoke(false);

    }

    public void StateAuthorityChanged()
    {
        // 내가 방금 새로운 방장이 되었다면?
        if (HasStateAuthority)
        {
            Debug.Log("[서버] 새로운 방장으로 임명되었습니다. 데이터를 청소합니다.");
            CleanUpDisconnectedPlayers();
        }
    }
    
    /// <summary>
    /// 현재 세션에 없는(이미 나간) 플레이어의 정보가 딕셔너리에 남아있다면 강제로 지웁니다.
    /// </summary>
    private void CleanUpDisconnectedPlayers()
    {
        // 지워야 할 유령 플레이어들을 담을 리스트
        List<PlayerRef> ghostPlayers = new List<PlayerRef>();

        // 딕셔너리를 순회하면서, 현재 접속 중인 플레이어 목록(Runner.ActivePlayers)에 없는 사람을 찾습니다.
        foreach (var item in Roles)
        {
            // 이 플레이어가 현재 살아있는 플레이어 목록에 없다면? (방금 나간 예전 방장 등)
            bool isPlayerActive = false;
            foreach (var activePlayer in Runner.ActivePlayers)
            {
                if (item.Key == activePlayer)
                {
                    isPlayerActive = true;
                    break;
                }
            }

            if (!isPlayerActive)
            {
                ghostPlayers.Add(item.Key);
            }
        }

        // 찾아낸 유령 플레이어들을 딕셔너리에서 안전하게 제거합니다.
        foreach (var ghost in ghostPlayers)
        {
            Roles.Remove(ghost);
            //Debug.Log($"[서버 청소] 나간 플레이어({ghost})의 찌꺼기 데이터를 삭제했습니다.");
        }
    }
}



