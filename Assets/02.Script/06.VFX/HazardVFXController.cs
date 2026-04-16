using UnityEngine;

public class HazardVFXController : MonoBehaviour
{
    private void OnEnable()
    {
        // 3초 대기가 끝난 재난 발생 이벤트를 듣습니다.
        HazardManager.OnHazardTriggered += HandleHazardTriggered;
    }

    private void OnDisable()
    {
        HazardManager.OnHazardTriggered -= HandleHazardTriggered;
    }

    private void HandleHazardTriggered(HazardData data)
    {
        // 눈사태 데이터라면?
        if (data is AvalancheData avalancheData)
        {
            if (avalancheData.PathSystem != null)
            {
                // 프리팹을 생성하지 않고, 씬에 있는 시스템의 Play 함수를 호출합니다!
                avalancheData.PathSystem.PlayAvalanche();
                Debug.Log($"[HazardVFXController] 3초 대기 완료. {avalancheData.PathSystem.gameObject.name} 눈사태 재생 시작!");
            }
        }
        // (낙석이나 눈보라 처리는 여기에 else if 로 추가)
    }
}