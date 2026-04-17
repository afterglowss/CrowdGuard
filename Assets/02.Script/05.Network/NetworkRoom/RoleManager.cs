using System;
using Fusion;


public class RoleManager : NetworkBehaviour
{
    public static RoleManager Instance{ get; private set;} 
    
    public enum Role
    {
        None = 0,
        Leader,
        Supporter
    }

    [Networked,OnChangedRender(nameof(RoleChanged))] public NetworkDictionary<PlayerRef,Role> Roles { get; }

    public event Action<bool> OnRoleAccepted;
    
    // 싱글턴 설정
    public override void Spawned()
    {
        Instance = this;
        RoleChanged();
        base.Spawned();
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
    public void RPC_SetPlayerRole(PlayerRef player, Role role)
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
    
    /// <summary>
    /// 퇴장한 플레이어의 역할 제거
    /// </summary>
    /// <param name="player"></param>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RemovePlayer(PlayerRef player)
    {
        Roles.Remove(player);
    }

    /// <summary>
    /// 역할 변화 시 게임 시작 가능한지 판별하기
    /// </summary>
    private void RoleChanged()
    {
        var role = Role.None;

        foreach (var item in Roles)
        {
            if (role == Role.None)
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
}
