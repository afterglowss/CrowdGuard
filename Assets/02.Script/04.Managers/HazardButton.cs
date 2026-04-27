using UnityEngine;

public class HazardButton : MonoBehaviour
{
    [Header("Avalanche Target")]
    [Tooltip("이 버튼을 잡았을 때 작동시킬 씬 내의 눈사태 시스템을 연결하세요.")]
    public int avalancheIndex = 0;

    /// <summary>
    /// XR 컨트롤러로 그랩(Select)했을 때 실행될 함수
    /// </summary>
    public void TriggerAvalanche()
    {
        HazardManager.Instance?.TriggerAvalanche(avalancheIndex);
    }
}