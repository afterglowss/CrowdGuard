using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Capstone.Photon
{
    public class LobbyManager : MonoBehaviour
    {
        bool isLoading = false;
        public void StartGame()
        {
            if (isLoading) return;
            _ = StartGameAsync();
        }

        private async Task StartGameAsync()
        {
            isLoading = true;
            await PhotonManager.Instance.StartGame();
            isLoading = false;
        }
    }
}
