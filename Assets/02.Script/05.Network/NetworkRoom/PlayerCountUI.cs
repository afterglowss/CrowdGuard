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
        foreach (var player in roleManager.Roles)
        {
            if (player.Value == Role.Role.Supporter)
            {
                playerRoleText.text += "Supporter : ";
            }
            else if (player.Value == Role.Role.Leader)
            {
                playerRoleText.text += "Leader : ";
            }
            else
            {
                break;
            }
            playerRoleText.text += $"{player.Key}\n";
            
        }
        interactor.SetActive(isRoleSet);
    }

    private void OnDestroy()
    {
        roleManager.OnRoleAccepted -= RoleSet;
    }
}
