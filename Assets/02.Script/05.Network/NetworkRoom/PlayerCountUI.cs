using System;
using TMPro;
using UnityEngine;

public class PlayerCountUI : MonoBehaviour
{

    public RoleManager roleManager;
    public GameObject interactor;
    public TextMeshPro playerRoleText;
    private void Start()
    {
        roleManager.OnRoleAccepted += RoleSet;
    }

    private void RoleSet(bool isRoleSet)
    {
        playerRoleText.text = "";
        foreach (var role in roleManager.Roles)
        {
            if (role.Value == RoleManager.Role.Supporter)
            {
                playerRoleText.text += "Supporter : ";
            }
            else if (role.Value == RoleManager.Role.Leader)
            {
                playerRoleText.text += "Leader : ";
            }
            else
            {
                break;
            }
            playerRoleText.text += $"{role.Key}\n";
            
        }
        interactor.SetActive(isRoleSet);
    }

    private void OnDestroy()
    {
        roleManager.OnRoleAccepted -= RoleSet;
    }
}
