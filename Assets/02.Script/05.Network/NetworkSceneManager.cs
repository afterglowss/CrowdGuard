using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

public class NetworkSceneManager : NetworkSceneManagerDefault
{
    [SerializeField] private FadeUI fadeUI;
    public override NetworkSceneAsyncOp LoadScene(SceneRef sceneRef, NetworkLoadSceneParameters parameters)
    {
        return base.LoadScene(sceneRef, parameters);
        // 씬 전환 Task 생성
        /*Task sceneLoadTask = PerformSceneLoadWithFade(sceneRef, parameters);

        // Task 객체 반환
        return NetworkSceneAsyncOp.FromTask(sceneRef,sceneLoadTask);*/
    }

    private async Task PerformSceneLoadWithFade(SceneRef sceneRef, NetworkLoadSceneParameters parameters)
    {
        // Fade Out 대기
        if (fadeUI != null)
        {
            await fadeUI.PlayFadeOut();
        }

        // Fusion 씬 로딩 대기
        var op = base.LoadScene(sceneRef, parameters);
        await op; 

        // 완료 시 Fade In
        if (fadeUI != null)
        {
            await fadeUI.PlayFadeIn();
        }
    }
    
}
