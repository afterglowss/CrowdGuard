using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Capstone.Photon.Game
{
    public class GamePlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public GameObject playerPrefab;
        public LocalPlayerController localController;
        public Room.PlayerManager playerManager;

        private NetworkRunner _currentRunner;
        private void Start()
        {
            if (PhotonManager.Instance)
            {
                _currentRunner = PhotonManager.Instance.InstanceRunner;
                _currentRunner.AddCallbacks(this);
            }
        }
        
        private void OnDestroy()
        {
            _currentRunner.RemoveCallbacks(this);
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            var playerModel = runner.Spawn(playerPrefab,Vector3.zero,Quaternion.identity,runner.LocalPlayer);
            if (playerModel.TryGetComponent(out PlayerModel model))
            {
                model.Init(localController);
            }
            Debug.Log($"LocalPlayer {runner.LocalPlayer} Model Set");
            //playerManager.RPC_AddPlayer(runner.LocalPlayer,playerModel);
        }


        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player {runner.ActivePlayers.ToList().Count} remain");
            if (runner.ActivePlayers.ToList().Count < 2)
            {
                Debug.Log("게임을 진행할 수 없습니다. 메인화면으로 이동합니다.");
                runner.Shutdown();
                SceneManager.LoadScene(0);
            }
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