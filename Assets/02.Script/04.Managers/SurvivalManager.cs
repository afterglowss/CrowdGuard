using System;
using Fusion;
using UnityEngine;

public class SurvivalManager : NetworkBehaviour
{
    public static SurvivalManager Instance { get; private set; }

    [Header("동결 게이지 설정")]
    public const float MAX_FREEZE_GAUGE = 600f;
    public const float EFFECT_START_GAUGE = 120f; // 120부터 시각 효과 시작
    // 👇 인스펙터에서 편하게 드래그할 수 있도록 Range 슬라이더 추가!

    [Networked]
    public float currentFreezeGauge { get; set; }

    // [Networked]: 한 클라이언트에서 설정해도 양쪽에 동기화됨
    [Networked, Tooltip("눈보라 또는 로프 패널티 시 급격한 증가를 적용할 지 여부")]
    public bool isRapidFreezing { get; set; }

    [Networked, Tooltip("텐트 랜턴 켤 때 회복 중 여부")]
    public bool isRestoring { get; set; }

    private bool isPlayerFrozen = false;

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
            currentFreezeGauge -= restoreRate * deltaTime;
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
        
        // 0.0 ~ 1.0 사이를 절대 벗어나지 않게 Clamp01로 안전장치 추가
        float freezeRatio = Mathf.Clamp01(currentEffectValue / effectRange);
        
        // 글로벌 셰이더 변수 쏘기
        Shader.SetGlobalFloat("_FreezingAmount", freezeRatio);
    }

    public void SetRapidFreezing(bool isRapid)
    {
        isRapidFreezing = isRapid;
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

    private void TriggerFreezeDeath()
    {
        isPlayerFrozen = true;
        OnPlayerFrozen?.Invoke();
        Debug.LogWarning("[SurvivalManager] 동결!");
    }
}