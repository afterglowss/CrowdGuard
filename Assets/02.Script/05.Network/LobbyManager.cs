using System;
using UnityEngine;

namespace Capstone.Photon
{
    public class LobbyManager : MonoBehaviour
    {
        public void StartGame()
        {
            var result =  PhotonManager.Instance.StartGame();
        }
    }
}
