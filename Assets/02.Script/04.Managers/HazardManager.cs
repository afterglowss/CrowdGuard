using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// --- Payload Data Classes (재난별 다형성 데이터 구조) ---
public abstract class HazardData
{
    public Vector3 Location;
}

public class AvalancheData : HazardData
{
    public AvalanchePathSystem PathSystem;
    public float Width;
    public float Speed;
}

public class BlizzardData : HazardData
{
    public float Duration;
    public float FreezeMultiplier;
}

public class RockfallData : HazardData
{
    public int RockCount;
    public float FallRadius;
}

public class HazardManager : MonoBehaviour
{
    public static HazardManager Instance { get; private set; }

    public static event Action<HazardData> OnHazardWarning;
    public static event Action<HazardData> OnHazardTriggered;

    [Header("눈사태 시스템 (씬에 배치된 AvalanchePathSystem 오브젝트)")]
    public List<AvalanchePathSystem> avalancheSystems = new List<AvalanchePathSystem>();

    [Header("낙석 프리팹 목록 (인덱스 0번부터)")]
    public List<GameObject> rockfallPrefabs = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ===================== 인덱스 기반 트리거 =====================

    /// <summary>
    /// 인덱스로 눈사태 시스템을 선택해 트리거합니다. HazardButton 등에서 호출하세요.
    /// </summary>
    public void TriggerAvalanche(int index)
    {
        if (index < 0 || index >= avalancheSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] avalancheSystems[{index}] 없음. 인스펙터 리스트를 확인하세요.");
            return;
        }

        AvalanchePathSystem system = avalancheSystems[index];
        var data = new AvalancheData
        {
            Location = system.transform.position,
            PathSystem = system
        };
        TriggerHazardExternal(data);
    }

    /// <summary>
    /// 인덱스로 낙석 프리팹을 선택해 지정 위치에 스폰합니다.
    /// </summary>
    public void TriggerRockfall(int index, Vector3 location, int rockCount = 5, float fallRadius = 2f)
    {
        if (index < 0 || index >= rockfallPrefabs.Count)
        {
            Debug.LogWarning($"[HazardManager] rockfallPrefabs[{index}] 없음. 인스펙터 리스트를 확인하세요.");
            return;
        }

        var data = new RockfallData
        {
            Location = location,
            RockCount = rockCount,
            FallRadius = fallRadius
        };
        TriggerHazardExternal(data);
    }

    /// <summary>
    /// 눈보라를 직접 트리거합니다.
    /// </summary>
    public void TriggerBlizzard(Vector3 location, float duration = 5f, float freezeMultiplier = 4f)
    {
        var data = new BlizzardData
        {
            Location = location,
            Duration = duration,
            FreezeMultiplier = freezeMultiplier
        };
        TriggerHazardExternal(data);
    }

    // ===================== 공통 시퀀스 =====================

    /// <summary>
    /// 외부에서 직접 Data 객체를 만들어 넘길 때 사용 (HazardButton 레거시 지원)
    /// </summary>
    public void TriggerHazardExternal(HazardData hazardData)
    {
        StartCoroutine(HazardSequenceRoutine(hazardData));
    }

    private IEnumerator HazardSequenceRoutine(HazardData data)
    {
        OnHazardWarning?.Invoke(data);
        Debug.Log($"[HazardManager] 경고! 3초 후 {data.GetType().Name} 발생 예정! (위치: {data.Location})");

        yield return new WaitForSeconds(3f);

        OnHazardTriggered?.Invoke(data);
        Debug.Log($"[HazardManager] {data.GetType().Name} 발생! (위치: {data.Location})");

        switch (data)
        {
            case BlizzardData blizzard:
                StartCoroutine(ApplyBlizzardPenaltyRoutine(blizzard));
                break;
            case AvalancheData avalanche:
                // PlayAvalanche는 HazardVFXController가 OnHazardTriggered를 받아 처리
                break;
            case RockfallData rockfall:
                SpawnRockfall(rockfall);
                break;
        }
    }

    // ===================== 재난별 로직 =====================

    private IEnumerator ApplyBlizzardPenaltyRoutine(BlizzardData data)
    {
        if (SurvivalManager.Instance != null)
        {
            SurvivalManager.Instance.SetRapidFreezing(true);
            yield return new WaitForSeconds(data.Duration);
            SurvivalManager.Instance.SetRapidFreezing(false);
            Debug.Log($"[HazardManager] 눈보라 종료 ({data.Duration}초).");
        }
    }

    private void SpawnRockfall(RockfallData data)
    {
        if (rockfallPrefabs.Count == 0)
        {
            Debug.LogWarning("[HazardManager] rockfallPrefabs 리스트가 비어있습니다.");
            return;
        }

        // 기본 프리팹(0번)으로 rockCount만큼 랜덤 위치에 스폰
        GameObject prefab = rockfallPrefabs[0];
        for (int i = 0; i < data.RockCount; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * data.FallRadius;
            Vector3 spawnPos = data.Location + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Instantiate(prefab, spawnPos, UnityEngine.Random.rotation);
        }
    }

    // ===================== 주기 재난 =====================

    public void StartCyclicBlizzard(Vector3 centerLocation, float intervalSeconds = 60f)
    {
        StartCoroutine(CyclicBlizzardRoutine(centerLocation, intervalSeconds));
    }

    private IEnumerator CyclicBlizzardRoutine(Vector3 loc, float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            TriggerBlizzard(loc);
        }
    }

    public void StartCyclicRockfall(Vector3 targetLocation, int prefabIndex = 0, float intervalSeconds = 20f)
    {
        StartCoroutine(CyclicRockfallRoutine(targetLocation, prefabIndex, intervalSeconds));
    }

    private IEnumerator CyclicRockfallRoutine(Vector3 loc, int prefabIndex, float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            TriggerRockfall(prefabIndex, loc);
        }
    }
}
