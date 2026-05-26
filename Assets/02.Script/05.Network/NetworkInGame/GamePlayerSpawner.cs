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
        /// <summary>
        /// 역할이 존재할 때 사용하는 XR Origin 오브젝트
        /// </summary>
        public GameObject controllerObject;

        /// <summary>
        /// 역할이 없을 때 사용하는 XR Origin
        /// </summary>
        public GameObject spectatorObject;
        

        private NetworkRunner _currentRunner;
        private void Start()
        {
            if (!PhotonManager.Instance) return;
            _currentRunner = PhotonManager.Instance.InstanceRunner;
            _currentRunner.AddCallbacks(this);
        }
        
        private void OnDestroy()
        {
            if (_currentRunner)
            {
                _currentRunner.RemoveCallbacks(this);
            }
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner.IsSharedModeMasterClient)
            {
                runner.SessionInfo.IsOpen = false;
            }

            if (RoleManager.Instance && RoleManager.Instance.Roles.ContainsKey(runner.LocalPlayer))
            {
                var obj = Instantiate(controllerObject);
                var playerModel = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
                if (playerModel.TryGetComponent(out GamePlayerModel model))
                {
                    if (obj.TryGetComponent<LocalPlayerController>(out var controller))
                    {
                        model.Init(controller);
                    }

                    // IceAxe 참조를 PlayerController에 주입 (네트워크 스폰 이후 타이밍 보정)
                    var playerController = obj.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.InjectAxes(model.leftIceAxe, model.rightIceAxe);
                    }
                    else
                    {
                        Debug.LogWarning("[GamePlayerSpawner] localController 오브젝트에 PlayerController 컴포넌트가 없습니다!");
                    }
                }
                ScreenEffectManager.Instance.Init();
            }
            else
            {
                Instantiate(spectatorObject);
            }
            
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