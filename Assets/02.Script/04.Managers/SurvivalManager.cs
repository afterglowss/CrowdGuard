using System;
using Fusion;
using UnityEngine;

public class SurvivalManager : NetworkBehaviour
{
    public static SurvivalManager Instance { get; private set; }

    [Header("동결 게이지 설정")]
    public const float MAX_FREEZE_GAUGE = 600f;
    public const float EFFECT_START_GAUGE = 60f; // 60부터 시각 효과 시작
    // 👇 인스펙터에서 편하게 드래그할 수 있도록 Range 슬라이더 추가!

    [Networked]
    public float currentFreezeGauge { get; set; }

    // [Networked]: 한 클라이언트에서 설정해도 양쪽에 동기화됨
    [Networked, Tooltip("눈보라 또는 로프 패널티 시 급격한 증가를 적용할 지 여부")]
    public bool isRapidFreezing { get; set; }

    [Networked, Tooltip("텐트 랜턴 켤 때 회복 중 여부")]
    public bool isRestoring { get; set; }

    [Networked, Tooltip("텐트 안(쉘터)에 있어 동결 게이지 증가를 멈출지 여부")]
    public bool isSheltered { get; set; }

    private bool isPlayerFrozen = false;
    public bool IsPlayerFrozen => isPlayerFrozen;

    [Tooltip("초당 동결 게이지 회복량 (예: 100이면 600 회복에 6초 소요)")]
    public float restoreRate = 100f;

    public event Action<float> OnFreezeGaugeChanged;
    public event Action OnPlayerFrozen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

#if UNITY_EDITOR
    [Range(0f, 600f)]
    public float editorFreezeGauge = 0f;
    // 🚨 [핵심 추가] 인스펙터에서 값을 수정할 때마다 자동으로 불리는 유니티 마법의 함수!
    // (Play 상태가 아닐 때도 작동합니다)
    private void OnValidate()
    {
        // 인스펙터에서 드래그할 때마다 셰이더를 강제로 업데이트합니다.
        UpdateShaderEffect(editorFreezeGauge); 
    }
    #endif

    /// <summary>
    /// 네트워크 연결 상태에서 게이지 업데이트 (State Authority만 실행)
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // StateAuthority(이 오브젝트를 소유한 클라이언트)만 게이지를 계산
        if (!HasStateAuthority) return;

        TickGauge(Runner.DeltaTime);
    }

    /// <summary>
    /// 실제 게이지 증감 로직. Update/FixedUpdateNetwork 양쪽에서 공용으로 사용.
    /// </summary>
    private void TickGauge(float deltaTime)
    {
        if (isPlayerFrozen) return;

        if (isRestoring)
        {
            // 랜턴 회복은 쉘터 여부와 무관하게 우선 적용
            currentFreezeGauge -= restoreRate * deltaTime;
        }
        else if (isSheltered)
        {
            // 텐트 안: 게이지를 그대로 고정 (증가도 회복도 없음)
        }
        else
        {
            float increaseRate = isRapidFreezing ? 4f : 1f;
            currentFreezeGauge += increaseRate * deltaTime;
        }

        currentFreezeGauge = Mathf.Clamp(currentFreezeGauge, 0f, MAX_FREEZE_GAUGE);
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);

        if (currentFreezeGauge >= MAX_FREEZE_GAUGE)
            TriggerFreezeDeath();
    }

    public override void Render()
    {
        // 매 프레임 셰이더 업데이트
        UpdateShaderEffect(currentFreezeGauge);
    }

    // 🚨 [핵심 추가] 셰이더 계산 로직을 밖으로 뺐습니다.
    private void UpdateShaderEffect(float value)
    {
        float effectRange = MAX_FREEZE_GAUGE - EFFECT_START_GAUGE;
        float currentEffectValue = Mathf.Max(0f, value - EFFECT_START_GAUGE);
        
        // 비선형 커브: 초반 게이지도 셰이더에 충분히 큰 값을 전달해 VR FOV 안쪽까지 서리가 표시되도록 함
        // 지수를 낮출수록 초반에 더 강하게 표시 (0.5=sqrt, 0.3=현재, 0.2=매우 강함)
        // 선형 대비 예시 → Gauge 200: 0.17 → 0.60 / Gauge 300: 0.38 → 0.72
        float freezeRatio = Mathf.Pow(Mathf.Clamp01(currentEffectValue / effectRange), 0.3f);
        
        // 글로벌 셰이더 변수 쏘기
        Shader.SetGlobalFloat("_FreezingAmount", freezeRatio);
    }

    public void SetRapidFreezing(bool isRapid)
    {
        isRapidFreezing = isRapid;
    }

    /// <summary>
    /// 리스폰 시 동결 게이지·상태·셰이더를 전부 초기화합니다.
    /// 로컬 bool은 모든 클라이언트에서, 네트워크 변수는 StateAuthority에서만 리셋합니다.
    /// </summary>
    public void ResetAfterRespawn()
    {
        isPlayerFrozen = false;

        if (HasStateAuthority)
        {
            currentFreezeGauge = 0f;
            isRapidFreezing    = false;
            isRestoring        = false;
            isSheltered        = false;
        }

        // Render()를 기다리지 않고 즉시 셰이더 초기화
        UpdateShaderEffect(0f);
        OnFreezeGaugeChanged?.Invoke(0f);
    }

    // 👇 [핵심] 랜턴을 켜고 끌 때 외부에서 호출할 함수
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetRestoringState(bool state)
    {
        
        isRestoring = state;
        Debug.Log(state ? "[SurvivalManager] 랜턴 불이 켜져 몸을 녹입니다." : "[SurvivalManager] 회복을 중단합니다.");
        
        isPlayerFrozen = false; 
        
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);
    }

    /// <summary>
    /// 텐트 입장/퇴장 시 호출. 텐트 안에서는 동결 게이지 증가를 멈춥니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetSheltered(bool state)
    {
        isSheltered = state;
        Debug.Log(state ? "[SurvivalManager] 텐트 진입 — 동결 게이지 정지" : "[SurvivalManager] 텐트 퇴장 — 동결 게이지 재개");
    }

    private void TriggerFreezeDeath()
    {
        isPlayerFrozen = true;
        Debug.LogWarning("[SurvivalManager] 동결! 텐트 세이브 포인트로 리스폰합니다.");
        // StateAuthority에서만 호출됨 → 전 클라이언트에 동결 사망 처리를 지시한다.
        RPC_OnFreezeDeath();
    }

    /// <summary>
    /// 동결 사망 처리를 전 클라이언트에 동시 실행합니다.
    /// 각 클라이언트는 세이브 포인트를 텐트로 복원하고 로컬 플레이어를 추락시킵니다.
    ///
    /// [주의] NetworkTriggered = true 로 설정한 채 ChangeState 를 호출해
    ///        FallingState.Enter()의 RPC_TriggerPartnerFall 재발송을 억제합니다.
    ///        (두 플레이어가 이미 모두 이 RPC로 추락하므로 추가 전파 불필요)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnFreezeDeath()
    {
        OnPlayerFrozen?.Invoke();

        // 세이브 포인트를 텐트로 복원 — 이미 RPC 내부이므로 로컬 업데이트만 수행
        if (SavePointManager.Instance != null)
            SavePointManager.Instance.LocalRevertToTentSavePoint();

        // 로컬 플레이어 추락 트리거
        var controller = PlayerController.LocalInstance;
        if (controller == null) return;
        if (controller.CurrentState == controller.FallingState) return;

        PlayerFallingState.NetworkTriggered = true;
        controller.ChangeState(controller.FallingState);
        PlayerFallingState.NetworkTriggered = false;
    }
}