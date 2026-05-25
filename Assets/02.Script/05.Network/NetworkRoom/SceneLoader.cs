using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class SceneLoader : NetworkBehaviour
    {
        public void RequestChangeLevel(int sceneIndex)
        {
            Debug.Log($"Request to Change level{sceneIndex}");
            RPC_ChangeLevel(sceneIndex);
        }


        [Rpc(RpcSources.All,RpcTargets.All)]
        public void RPC_ChangeLevel(int sceneIndex)
        {
            Debug.Log($"Changing level to {sceneIndex}");
            if (Runner.IsSharedModeMasterClient)
            {
                Debug.Log("Im master");
                Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            }
        }
    }
}