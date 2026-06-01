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

    // ── 눈보라 시각/청각: XR Rig의 PlayerBlizzardVisual에 위임 (네트워크 X) ──
    // 시각효과는 "그 공간에 실제로 있는 로컬 플레이어"에게만 보여야 한다.
    // XR Rig 카메라는 로컬 플레이어에게만 생성되므로, 로컬 Rig 하나만 토글하면 됨.
    // (구역형/이벤트형 공통. 참조 카운트는 PlayerBlizzardVisual 내부에서 관리)

    // ── 눈보라 동결 집계 (네트워크 O, StateAuthority 전용) ──
    // 어느 클라이언트의 플레이어든 눈보라 존에 들어가 있으면 공유 동결게이지를 가속.
    // 플레이어별 점유 카운트를 두어 겹치는 Zone에도 안전.
    private readonly Dictionary<PlayerRef, int> _blizzardOccupancy = new Dictionary<PlayerRef, int>();

    // 구역형 눈보라 센서 신호 (로컬 전용). 센서가 구독해 상시 100% WARNING을 표시.
    public static event Action OnBlizzardZoneSensorOn;
    public static event Action OnBlizzardZoneSensorOff;
    private int _blizzardZoneSensorCount = 0;

    /// <summary>
    /// 로컬 플레이어가 눈보라에 진입했을 때 호출. 로컬 Rig의 눈보라 연출 ON (Snow OFF).
    /// 시각/청각 전용 — 이벤트형(HazardSequenceRoutine)이 사용.
    /// </summary>
    public void LocalBlizzardEnter()
    {
        if (PlayerBlizzardVisual.LocalInstance != null)
            PlayerBlizzardVisual.LocalInstance.Enter();
    }

    /// <summary>
    /// 로컬 플레이어가 눈보라에서 이탈했을 때 호출. 마지막 이탈 시 평상시(Snow ON)로 복귀.
    /// </summary>
    public void LocalBlizzardExit()
    {
        if (PlayerBlizzardVisual.LocalInstance != null)
            PlayerBlizzardVisual.LocalInstance.Exit();
    }

    /// <summary>
    /// 구역형 눈보라 진입(BlizzardZone). 시각/청각(Rig)에 더해 센서 신호도 발생.
    /// 여러 존이 겹쳐도 첫 진입에만 센서 신호 ON.
    /// </summary>
    public void LocalBlizzardZoneEnter()
    {
        LocalBlizzardEnter();
        _blizzardZoneSensorCount++;
        if (_blizzardZoneSensorCount == 1) OnBlizzardZoneSensorOn?.Invoke();
    }

    /// <summary>
    /// 구역형 눈보라 이탈(BlizzardZone). 마지막 이탈에만 센서 신호 OFF.
    /// </summary>
    public void LocalBlizzardZoneExit()
    {
        LocalBlizzardExit();
        _blizzardZoneSensorCount = Mathf.Max(0, _blizzardZoneSensorCount - 1);
        if (_blizzardZoneSensorCount == 0) OnBlizzardZoneSensorOff?.Invoke();
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
                // 시각/청각: 각 클라이언트의 로컬 Rig를 blizzardDuration 동안 켰다 끔
                StartCoroutine(EventBlizzardVisualRoutine(blizzardDuration));
                // 동결: StateAuthority만 공유 게이지 가속
                if (HasStateAuthority)
                    StartCoroutine(ApplyBlizzardPenaltyRoutine());
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

    /// <summary>
    /// 이벤트형 눈보라의 로컬 시각/청각을 duration 동안 유지합니다.
    /// 각 클라이언트에서 자기 Rig를 토글하므로 로컬 처리.
    /// </summary>
    private IEnumerator EventBlizzardVisualRoutine(float duration)
    {
        LocalBlizzardEnter();
        yield return new WaitForSeconds(duration);
        LocalBlizzardExit();
    }

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
