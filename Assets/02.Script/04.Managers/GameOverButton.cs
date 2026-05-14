using UnityEngine;

/// <summary>
/// XR 컨트롤러로 이 오브젝트를 그랩(Select)하면 게임 오버를 모든 플레이어에게 알립니다.
/// HazardButton과 동일한 구조로, XR Interactable의 Select 이벤트에 TriggerGameOver()를 연결하세요.
/// </summary>
public class GameOverButton : MonoBehaviour
{
    /// <summary>
    /// XR 컨트롤러로 그랩(Select)했을 때 실행될 함수
    /// </summary>
    public void TriggerGameOver()
    {
        GameOverManager.Instance?.RPC_NotifyGameOver();
    }
}
