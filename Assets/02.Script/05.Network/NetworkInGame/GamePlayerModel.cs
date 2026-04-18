using Capstone.Photon.Game;
using Fusion;
using UnityEngine;

public class GamePlayerModel : PlayerModel
{
    public static GamePlayerModel LocalPlayerModel;
    [Networked] private Role.Role CurrentRole { get; set; }
    
    public override void Spawned()
    {
        base.Spawned();
        // TODO : 오브젝트 등록하기 현재 권한, 해당 오브젝트
        Debug.Log($"{RoleManager.Instance}");
        Debug.Log($"{RoleManager.Instance.Roles[Object.StateAuthority]}");
        CurrentRole = RoleManager.Instance.Roles[Object.StateAuthority];
        Debug.Log($"{CurrentRole}");
        PlayerManager.Instance.SetPlayer(CurrentRole, Object);
    }

    public override void Init(LocalPlayerController controller)
    {
        base.Init(controller);
        LocalPlayerModel = this;
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (LocalPlayerModel == this)
        {
            LocalPlayerModel = null;
        }
    }
}
