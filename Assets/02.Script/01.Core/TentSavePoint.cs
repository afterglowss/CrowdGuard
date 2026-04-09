using System.Collections;
using UnityEngine;

public class TentSavePoint : MonoBehaviour
{
    [Tooltip("텐트에서 나올 때 자동으로 세이브/부활 처리될 앵커 위치")]
    public Transform autoAnchorSpawnPos;

    [Tooltip("(선택) 시각적으로 세이브를 알리기 위해 텐트 옆에 소환할 앵커 프리팹")]
    public GameObject anchorPrefab;

    private bool isInteracting = false;

    /// <summary>
    /// XR 시스템(그립 트리거 등)에서 텐트와 상호작용할 때 외부에서 호출
    /// </summary>
    public void InteractWithTent()
    {
        if (isInteracting) return;
        StartCoroutine(TentSequenceRoutine());
    }

    private IEnumerator TentSequenceRoutine()
    {
        isInteracting = true;

        // 1. 화면 암전 처리 (Fade Out: 밝은 화면 -> 검은 화면)
        Debug.Log("[TentSavePoint] 텐트 진입 완료. 화면을 암전합니다.");
        if (ScreenEffectManager.Instance != null)
        {
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(1f, false));
        }

        // 텐트 안에서 정비하는 시간 대기
        yield return new WaitForSeconds(1.5f);

        // 2. 동결 수치 완전 회복
        if (SurvivalManager.Instance != null)
        {
            SurvivalManager.Instance.RestoreFreezeGauge();
        }

        // 3. 앵커 보급 (최소 5개 유지)
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SupplyAnchorsAtTent();
        }

        // 4. 세이브 포인트 강제 지정
        Vector3 savePosition = autoAnchorSpawnPos != null ? autoAnchorSpawnPos.position : transform.position;
        if (SavePointManager.Instance != null)
        {
            SavePointManager.Instance.ForceSetSavePoint(savePosition);
        }

        // (옵션) XR 팀에서 실제로 벽에 박힌 시각적 프리팹을 요구한다면 생성
        if (anchorPrefab != null && autoAnchorSpawnPos != null)
        {
            Instantiate(anchorPrefab, autoAnchorSpawnPos.position, autoAnchorSpawnPos.rotation);
        }

        Debug.Log("[TentSavePoint] 텐트 정비 완료. 화면을 다시 밝힙니다.");

        // 5. 화면 암전 해제 (Fade In: 검은 화면 -> 밝은 화면)
        if (ScreenEffectManager.Instance != null)
        {
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(1f, true));
        }
        
        isInteracting = false;
    }
}
