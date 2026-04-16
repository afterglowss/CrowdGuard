using Capstone.Photon;
using UnityEngine;

public class RoleSelector : MonoBehaviour
{
    public void SetPlayerLeader()
    {
        var local = PhotonManager.Instance.InstanceRunner.LocalPlayer;
        RoleManager.Instance.RPC_SetPlayerRole(local,RoleManager.Role.Leader);
    }
    public void SetPlayerSupporter()
    {
        var local = PhotonManager.Instance.InstanceRunner.LocalPlayer;
        RoleManager.Instance.RPC_SetPlayerRole(local,RoleManager.Role.Supporter);
    }
}
