using System;
using System.Collections;
using UnityEngine;

// --- Payload Data Classes (재난별 다형성 데이터 구조) ---
public abstract class HazardData
{
    public Vector3 Location;
    // 필요 시 모든 재난이 공유해야 할 공통 파라미터가 있다면 여기에 추가
}

public class AvalancheData : HazardData
{
    [Tooltip("눈사태의 영향을 받는 범위 폭")]
    public float Width;        // 예: 4명 너비
    public float Speed;        // 떨어지는 속도
}

public class BlizzardData : HazardData
{
    public float Duration;     // 기획서 기준 5초 기본
    public float FreezeMultiplier; // 동결 게이지 증가 배수
}

public class RockfallData : HazardData
{
    public int RockCount;      // 떨어질 바위 개수
    public float FallRadius;   // 낙석 발생 반경 (랜덤 스폰용)
}

public class HazardManager : MonoBehaviour
{
    public static HazardManager Instance { get; private set; }

    // 다형성을 이용한 단일 통합 전역 이벤트. 센서나 VFX 쪽에서 이를 구독합니다.
    public static event Action<HazardData> OnHazardWarning;   // 3초 전 예측/경고
    public static event Action<HazardData> OnHazardTriggered; // 실제 발생 시 타격 처리 등에 사용

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 외부(TriggerArea 등)에서 생성한 특화 Data 객체를 넘겨주어 재난 실행
    /// </summary>
    public void TriggerHazardExternal(HazardData hazardData)
    {
        StartCoroutine(HazardSequenceRoutine(hazardData));
    }

    /// <summary>
    /// 재난 발생 시퀀스 코루틴 (경고 3초 후 실제 재난 발생)
    /// </summary>
    private IEnumerator HazardSequenceRoutine(HazardData data)
    {
        // 1. 센서에 3초 전 경고 전달 (Data 전체를 넘기므로 센서 단에서 필요한 파라미터만 빼서 사용 가능)
        OnHazardWarning?.Invoke(data);
        Debug.Log($"[HazardManager] 경고! 3초 후 {data.GetType().Name} 발생 예정! (위치: {data.Location})");

        yield return new WaitForSeconds(3f);

        // 2. 실제 재난 발생 처리
        OnHazardTriggered?.Invoke(data);
        Debug.Log($"[HazardManager] {data.GetType().Name} 발생! (위치: {data.Location})");

        // 3. 재난별 특수 로직 분기 (C# 패턴 매칭)
        switch (data)
        {
            case BlizzardData blizzard:
                StartCoroutine(ApplyBlizzardPenaltyRoutine(blizzard));
                break;
            case AvalancheData avalanche:
                // 눈사태 전용 로직 (망가지는 장애물 생성 또는 물리 연산 등)
                break;
            case RockfallData rockfall:
                // 낙석 전용 로직 (ObjectPool에서 바위를 꺼내 무작위 투하 등)
                break;
        }
    }

    private IEnumerator ApplyBlizzardPenaltyRoutine(BlizzardData data)
    {
        if (SurvivalManager.Instance != null)
        {
            SurvivalManager.Instance.SetRapidFreezing(true);
            yield return new WaitForSeconds(data.Duration); // 하드코딩 5초 대신 Payload 데이터 참조
            SurvivalManager.Instance.SetRapidFreezing(false);
            Debug.Log($"[HazardManager] 눈보라 지속시간({data.Duration}초) 종료. 급격한 동결 페널티 해제.");
        }
    }

    // --- 주기에 따른 자동 발생 인터페이스 ---

    public void StartCyclicBlizzard(Vector3 centerLocation)
    {
        StartCoroutine(CyclicBlizzardRoutine(centerLocation));
    }

    private IEnumerator CyclicBlizzardRoutine(Vector3 loc)
    {
        while (true)
        {
            yield return new WaitForSeconds(60f); // 기획서: 특정 구간 1분마다 눈보라 발생
            
            // Payload Data 생성
            var blizzardData = new BlizzardData 
            { 
                Location = loc, 
                Duration = 5f, 
                FreezeMultiplier = 4f 
            };
            
            StartCoroutine(HazardSequenceRoutine(blizzardData));
        }
    }

    public void StartCyclicRockfall(Vector3 targetLocation)
    {
        StartCoroutine(CyclicRockfallRoutine(targetLocation));
    }

    private IEnumerator CyclicRockfallRoutine(Vector3 loc)
    {
        while (true)
        {
            yield return new WaitForSeconds(20f); // 기획서: 20초 주기 고정형 낙석
            
            // Payload Data 생성
            var rockfallData = new RockfallData 
            { 
                Location = loc, 
                RockCount = 5, 
                FallRadius = 2f 
            };
            
            StartCoroutine(HazardSequenceRoutine(rockfallData));
        }
    }
}
