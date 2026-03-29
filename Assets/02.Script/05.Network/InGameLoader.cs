using Fusion;
using UnityEngine.SceneManagement;

namespace Capstone.Fusion
{
    public class InGameLoader : NetworkBehaviour
    {
        public void ChangeLevel(int sceneIndex)
        {
            if (Object.HasStateAuthority)
            {
                Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            }
        }
    }
}