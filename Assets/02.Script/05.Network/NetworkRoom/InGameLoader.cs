using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class InGameLoader : NetworkBehaviour
    {
        public void RequestChangeLevel(int sceneIndex)
        {
            Debug.Log($"Request to Change level{sceneIndex}");
            RPC_ChangeLevel(sceneIndex);
        }


        [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
        public void RPC_ChangeLevel(int sceneIndex)
        {
            Debug.Log($"Changing level to {sceneIndex}");
            Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
        }
    }
}