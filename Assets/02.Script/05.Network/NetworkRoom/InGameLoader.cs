using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class InGameLoader : NetworkBehaviour
    {
        public void ChangeLevel(int sceneIndex)
        {
            Debug.Log($"Changing level to {sceneIndex}");
            if (Object.HasStateAuthority)
            {
                Runner.MakeDontDestroyOnLoad(RoleManager.Instance.gameObject);
                Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
                
            }
        }
    }
}