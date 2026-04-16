using UnityEngine;

public class HazardButton : MonoBehaviour
{
    [Header("Avalanche Target")]
    [Tooltip("이 버튼을 잡았을 때 작동시킬 씬 내의 눈사태 시스템을 연결하세요.")]
    public AvalanchePathSystem targetAvalanche;

    /// <summary>
    /// XR 컨트롤러로 그랩(Select)했을 때 실행될 함수
    /// </summary>
    public void TriggerAvalanche()
    {
        if (HazardManager.Instance == null) return;
        if (targetAvalanche == null)
        {
            Debug.LogWarning("[HazardButton] 연결된 AvalanchePathSystem이 없습니다! 인스펙터를 확인하세요.");
            return;
        }

        // 눈사태 시스템 데이터를 포장합니다.
        var data = new AvalancheData
        {
            Location = targetAvalanche.transform.position,
            PathSystem = targetAvalanche // 👈 핵심: 우리가 켤 시스템을 Manager에 넘겨줍니다.
        };

        // HazardManager에 3초 대기 큐를 넣습니다.
        HazardManager.Instance.TriggerHazardExternal(data);
        Debug.Log($"[HazardButton] 눈사태 트리거({targetAvalanche.gameObject.name}) 작동! (3초 후 쏟아집니다)");
    }
}