using Capstone.Photon;
using CrowdGuard.Climbing.Tools.Common;
using UnityEngine;

public class RoleSelector : MonoBehaviour
{
    public void SetPlayerLeader()
    {
        var local = PhotonManager.Instance.InstanceRunner.LocalPlayer;
        RoleManager.Instance.RPC_SetPlayerRole(local,PlayerRole.Leader);
    }
    public void SetPlayerSupporter()
    {
        var local = PhotonManager.Instance.InstanceRunner.LocalPlayer;
        RoleManager.Instance.RPC_SetPlayerRole(local,PlayerRole.Navigator);
    }
}
