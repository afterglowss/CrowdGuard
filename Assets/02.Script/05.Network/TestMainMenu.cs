using System;
using UnityEngine;

namespace Capstone.Photon
{
    public class TestMainMenu : MonoBehaviour
    {
        public void StartGame()
        {
            var result =  PhotonManager.Instance.StartGame();
        }
    }
}
