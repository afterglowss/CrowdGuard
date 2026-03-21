using System;
using System.Collections;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Photon
{
    public class PhotonManager : MonoBehaviour
    {
        /// <summary>
        /// 싱글톤 객체
        /// </summary>
        public static PhotonManager Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindObjectOfType<PhotonManager>();
                }
                return _instance;
            }
        }
        private static PhotonManager _instance;
        
        [SerializeField] private NetworkRunner runnerPrefab;
        
        public NetworkRunner InstanceRunner { get; private set; }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            NetworkRunner.CloudConnectionLost += OnCloudConnectionLost;

            if (_instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        
        void OnCloudConnectionLost(NetworkRunner runner, ShutdownReason reason, bool reconnecting)
        {
            Debug.Log($"Cloud Connection Lost: {reason} (Reconnecting: {reconnecting})");
            if (reconnecting)
            {
                StartCoroutine(WaitForReconnection(runner));
            }
        }
        
        IEnumerator WaitForReconnection(NetworkRunner runner)
        {
            yield return new WaitUntil(() => runner.IsInSession);
            Debug.Log("Reconnected to the Cloud!");
        }

        public async Task<bool> StartGame()
        {
            NetworkSceneInfo networkSceneInfo = default;
            networkSceneInfo.AddSceneRef(SceneRef.FromIndex(1),LoadSceneMode.Single, activeOnLoad: true);

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                PlayerCount = 2,
                Scene = networkSceneInfo,
            };

            InstanceRunner = InstantiateRunner("GameRunner");
 
            Debug.Log("Starting game...");
            try
            {
                var result = await InstanceRunner.StartGame(startGameArgs);

                if (!result.Ok)
                {
                    InstanceRunner = null;
                }

                return result.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during StartGame: {e.Message}");
                return false;
            }
        }
        NetworkRunner InstantiateRunner(string runnerName)
        {
            var runner = Instantiate(runnerPrefab);
            runner.name = runnerName;
            runner.ProvideInput = true;
            //runner.AddCallbacks(this);
        
            return runner;
        }
        /*#region UnuseCallbacks

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {

        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {

        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
        }


        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
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

        public void OnSceneLoadDone(NetworkRunner runner)
        {

        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {

        }

        #endregion*/

    }
}