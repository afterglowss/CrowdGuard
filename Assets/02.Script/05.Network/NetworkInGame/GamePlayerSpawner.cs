using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Capstone.Photon.Game
{
    public class GamePlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public GameObject playerPrefab;
        public LocalPlayerController localController;

        private void Start()
        {
            if (PhotonManager.Instance)
            {
                PhotonManager.Instance.InstanceRunner.AddCallbacks(this);
            }
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            var playerModel = runner.Spawn(playerPrefab,Vector3.zero,Quaternion.identity,runner.LocalPlayer);
            if (playerModel.TryGetComponent(out PlayerModel model))
            {
                model.Init(localController);
            }
            Debug.Log($"LocalPlayer {runner.LocalPlayer} Model Set");
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            
        }
        
        #region UnuseCallbacks
        
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            
        }
        
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {

        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {

        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {

        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {

        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
                byte[] token)
        {

        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress,
                NetConnectFailedReason reason)
        {
                
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {

        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
                ArraySegment<byte> data)
        {

        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key,
                float progress)
        {

        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {

        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {

        }

        public void OnConnectedToServer(NetworkRunner runner)
        {

        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {

        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {

        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {

        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {

        }

        #endregion
    }
}