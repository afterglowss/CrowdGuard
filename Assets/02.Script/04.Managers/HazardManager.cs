using Fusion;
using SimpleAudioManager;
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
    public int Index = -1;
}


public class RockfallData : HazardData
{
    /// <summary>HazardManager.rockSystems 리스트의 인덱스. -1이면 실제 낙석 없이 이벤트만 발생.</summary>
    public int Index = -1;
}

public class HazardManager : NetworkBehaviour
{
    public static HazardManager Instance { get; private set; }

    public static event Action<HazardData> OnHazardWarning;
    public static event Action<HazardData> OnHazardTriggered;

    [Header("눈사태 시스템 (씬에 배치된 AvalanchePathSystem 오브젝트)")]
    public List<AvalanchePathSystem> avalancheSystems = new List<AvalanchePathSystem>();

    [Header("낙석 오브젝트 목록 (씬에 배치된 FallingRock, 인덱스 0번부터)")]
    public List<FallingRock> rockSystems = new List<FallingRock>();

    [Header("눈보라 파티클 시스템 목록 (씬에 배치된 BlizzardSystem 오브젝트)")]
    public List<BlizzardSystem> blizzardSystems = new List<BlizzardSystem>();

    [Header("눈보라 공통 설정")]
    public float blizzardDuration = 5f;
    public float blizzardFreezeMultiplier = 4f;

    [Header("경고 설정")]
    [Tooltip("재난 발생 전 경고 시간(초). 이 시간 동안 센서 등 UI가 알림을 표시합니다.")]
    public float warningDuration = 3f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ===================== 인덱스 기반 트리거 =====================

    /// <summary>
    /// 인덱스로 눈사태 시스템을 선택해 트리거합니다. HazardButton 등에서 호출하세요.
    /// </summary>
    [Rpc(RpcSources.All,RpcTargets.All)]
    public void RPC_TriggerAvalanche(int index)
    {
        if (index < 0 || index >= avalancheSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] avalancheSystems[{index}] 없음. 인스펙터 리스트를 확인하세요.");
            return;
        }

        Debug.Log("AvalancheTrigger");
        AvalanchePathSystem system = avalancheSystems[index];
        var data = new AvalancheData
        {
            // 피봇이 아닌 실제 경로 시작점(waypoints[0])을 사용
            Location = system.GetStartPosition(),
            PathSystem = system
        };
        TriggerHazardExternal(data);
    }

    /// <summary>
    /// 인덱스로 씬에 배치된 FallingRock을 활성화합니다. HazardButton 등에서 호출하세요.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_TriggerRockfall(int index)
    {
        if (index < 0 || index >= rockSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] rockSystems[{index}] 없음. 인스펙터 리스트를 확인하세요.");
            return;
        }

        Debug.Log("RockFall");
        var data = new RockfallData
        {
            Location = rockSystems[index].GetSpawnPosition(),
            Index = index
        };
        TriggerHazardExternal(data);
    }

    /// <summary>
    /// 인덱스로 FallingRock을 초기 위치로 되돌리고 비활성화합니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ResetRockfall(int index)
    {
        if (index < 0 || index >= rockSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] rockSystems[{index}] 없음.");
            return;
        }
        rockSystems[index].ResetRock();
        Debug.Log($"[HazardManager] rockSystems[{index}] 초기화 완료");
    }

    /// <summary>
    /// 인덱스로 눈보라 항목을 선택해 트리거합니다. HazardTriggerZone 등에서 호출하세요.
    /// </summary>
    [Rpc(RpcSources.All,RpcTargets.All)]
    public void RPC_TriggerBlizzard(int index)
    {
        if (index < 0 || index >= blizzardSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] blizzardSystems[{index}] 없음. 인스펙터 리스트를 확인하세요.");
            return;
        }

        Debug.Log("Blizzard");
        var data = new BlizzardData
        {
            Location = blizzardSystems[index].GetSpawnPosition(),
            Index    = index
        };
        TriggerHazardExternal(data);
    }

    /// <summary>
    /// 구역형 눈보라를 즉시 활성화합니다 (BlizzardZone 진입 시 호출).
    /// 파티클은 모든 클라이언트에서 무기한 재생, 동결 패널티는 StateAuthority에서만 적용.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ActivateBlizzardZone(int index)
    {
        if (index < 0 || index >= blizzardSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] blizzardSystems[{index}] 없음.");
            return;
        }
        blizzardSystems[index].Activate(); // duration=0 → Stop() 호출 전까지 유지
        if (HasStateAuthority && SurvivalManager.Instance != null)
            SurvivalManager.Instance.SetRapidFreezing(true);
        AudioManager.instance.PlaySFX(AudioManager.SFXType.Blizzard, transform);
        Debug.Log($"[HazardManager] BlizzardZone {index} 활성화");
    }

    /// <summary>
    /// 구역형 눈보라를 즉시 비활성화합니다 (BlizzardZone 퇴장 시 호출).
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_DeactivateBlizzardZone(int index)
    {
        if (index < 0 || index >= blizzardSystems.Count)
        {
            Debug.LogWarning($"[HazardManager] blizzardSystems[{index}] 없음.");
            return;
        }
        blizzardSystems[index].Stop();
        if (HasStateAuthority && SurvivalManager.Instance != null)
            SurvivalManager.Instance.SetRapidFreezing(false);
        Debug.Log($"[HazardManager] BlizzardZone {index} 비활성화");
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
        Debug.Log($"[HazardManager] 경고! {warningDuration}초 후 {data.GetType().Name} 발생 예정! (위치: {data.Location})");

        yield return new WaitForSeconds(warningDuration);

        OnHazardTriggered?.Invoke(data);
        Debug.Log($"[HazardManager] {data.GetType().Name} 발생! (위치: {data.Location})");

        switch (data)
        {
            case BlizzardData blizzard:
                if (blizzard.Index >= 0 && blizzard.Index < blizzardSystems.Count)
                    blizzardSystems[blizzard.Index].Activate(blizzardDuration);
                if (HasStateAuthority)
                    StartCoroutine(ApplyBlizzardPenaltyRoutine());
                AudioManager.instance.PlaySFX(AudioManager.SFXType.Blizzard, transform);
                break;
            case AvalancheData avalanche:
                // PlayAvalanche는 HazardVFXController가 OnHazardTriggered를 받아 처리
                break;
            case RockfallData rockfall:
                if (rockfall.Index >= 0 && rockfall.Index < rockSystems.Count)
                    rockSystems[rockfall.Index].Activate();
                break;
        }
    }

    // ===================== 재난별 로직 =====================

    private IEnumerator ApplyBlizzardPenaltyRoutine()
    {
        if (SurvivalManager.Instance != null)
        {
            SurvivalManager.Instance.SetRapidFreezing(true);
            yield return new WaitForSeconds(blizzardDuration);
            SurvivalManager.Instance.SetRapidFreezing(false);
            Debug.Log($"[HazardManager] 눈보라 종료 ({blizzardDuration}초).");
        }
    }

    // ===================== 주기 재난 =====================

    /// <summary>
    /// 반환된 Coroutine을 보관해두면 BlizzardZone 등 외부에서 StopCoroutine()으로 중단 가능.
    /// </summary>
    public Coroutine StartCyclicBlizzard(int blizzardIndex, float intervalSeconds = 60f)
    {
        return StartCoroutine(CyclicBlizzardRoutine(blizzardIndex, intervalSeconds));
    }

    private IEnumerator CyclicBlizzardRoutine(int index, float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            RPC_TriggerBlizzard(index);
        }
    }

    public Coroutine StartCyclicRockfall(int rockfallIndex, float intervalSeconds = 20f)
    {
        return StartCoroutine(CyclicRockfallRoutine(rockfallIndex, intervalSeconds));
    }

    private IEnumerator CyclicRockfallRoutine(int index, float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            RPC_TriggerRockfall(index);
        }
    }
}
