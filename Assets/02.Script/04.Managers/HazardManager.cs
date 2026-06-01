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

    // ── 구역형 눈보라: 로컬 시각/청각 (네트워크 X) ────────────────
    // 파티클·소리는 "그 공간에 실제로 있는 로컬 플레이어"에게만 보이고 들려야 한다.
    // 따라서 RPC가 아니라 각 클라이언트에서 로컬로만 호출한다.
    // 같은 index를 여러 Zone이 공유할 수 있으므로 로컬 참조 카운트로 관리.
    private readonly Dictionary<int, int> _localBlizzardRefCount = new Dictionary<int, int>();
    private readonly Dictionary<int, AudioSource> _localBlizzardSfx = new Dictionary<int, AudioSource>();

    // ── 구역형 눈보라: 동결 집계 (네트워크 O, StateAuthority 전용) ──
    // 어느 클라이언트의 플레이어든 눈보라 존에 들어가 있으면 공유 동결게이지를 가속.
    // 플레이어별 점유 카운트를 두어 겹치는 Zone에도 안전.
    private readonly Dictionary<PlayerRef, int> _blizzardOccupancy = new Dictionary<PlayerRef, int>();

    /// <summary>
    /// 로컬 플레이어가 눈보라 존에 들어갔을 때 호출 (BlizzardZone에서 직접 호출, RPC 아님).
    /// 해당 클라이언트에서만 파티클·소리를 켭니다.
    /// </summary>
    public void LocalEnterBlizzard(int index)
    {
        if (index < 0 || index >= blizzardSystems.Count) return;

        _localBlizzardRefCount.TryGetValue(index, out int count);
        count++;
        _localBlizzardRefCount[index] = count;

        if (count == 1)
        {
            blizzardSystems[index].Activate(); // duration=0 → Stop() 호출 전까지 유지
            AudioSource sfx = AudioManager.instance.PlaySFXLooping(
                AudioManager.SFXType.Blizzard, blizzardSystems[index].transform);
            if (sfx != null) _localBlizzardSfx[index] = sfx;
            Debug.Log($"[HazardManager] (로컬) BlizzardZone {index} 시각/청각 ON");
        }
    }

    /// <summary>
    /// 로컬 플레이어가 눈보라 존에서 나갔을 때 호출 (BlizzardZone에서 직접 호출, RPC 아님).
    /// </summary>
    public void LocalExitBlizzard(int index)
    {
        if (index < 0 || index >= blizzardSystems.Count) return;

        _localBlizzardRefCount.TryGetValue(index, out int count);
        count = Mathf.Max(0, count - 1);
        _localBlizzardRefCount[index] = count;

        if (count == 0)
        {
            blizzardSystems[index].Stop();
            if (_localBlizzardSfx.TryGetValue(index, out AudioSource sfx) && sfx != null)
                AudioManager.instance.StopSFXWithFade(sfx, 0.5f);
            _localBlizzardSfx.Remove(index);
            Debug.Log($"[HazardManager] (로컬) BlizzardZone {index} 시각/청각 OFF");
        }
    }

    /// <summary>
    /// 눈보라 존 점유 상태를 알립니다. 네트워크 세션이면 StateAuthority가 집계,
    /// 아니면(에디터 단독 등) 로컬에서 바로 동결 토글.
    /// </summary>
    public void NotifyBlizzardOccupancy(bool inside)
    {
        if (Runner != null && Runner.IsRunning)
            RPC_SetBlizzardOccupancy(inside);
        else
            SurvivalManager.Instance?.SetRapidFreezing(inside);
    }

    /// <summary>
    /// 플레이어별 눈보라 존 점유를 StateAuthority가 집계해 공유 동결게이지에 반영.
    /// 한 명이라도 존 안에 있으면 RapidFreezing ON.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetBlizzardOccupancy(bool inside, RpcInfo info = default)
    {
        PlayerRef player = info.Source;

        _blizzardOccupancy.TryGetValue(player, out int count);
        count = Mathf.Max(0, count + (inside ? 1 : -1));
        if (count == 0) _blizzardOccupancy.Remove(player);
        else            _blizzardOccupancy[player] = count;

        bool anyInside = _blizzardOccupancy.Count > 0;
        if (SurvivalManager.Instance != null)
            SurvivalManager.Instance.SetRapidFreezing(anyInside);

        Debug.Log($"[HazardManager] 눈보라 점유자 {_blizzardOccupancy.Count}명 → RapidFreeze={anyInside}");
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
