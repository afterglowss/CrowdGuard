using CrowdGuard.Climbing.Tools.Common;
using TMPro;
using UnityEngine;

namespace Capstone.Photon.Room
{
    public class PlayerRoleUI : MonoBehaviour
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
                if (player.Value == PlayerRole.Navigator)
                {
                    playerRoleText.text += "Navigator : ";
                }
                else if (player.Value == PlayerRole.Leader)
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
}
