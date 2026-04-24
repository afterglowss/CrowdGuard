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
    
    
    [Tooltip("눈보라 또는 로프 패널티 시 급격한 증가를 적용할 지 여부")]
    public bool isRapidFreezing = false;
    private bool isPlayerFrozen = false;

    // 👇 [추가] 텐트 안에서 회복 중인지 체크하는 변수와 회복 속도
    public bool isRestoring = false;
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
        UpdateShaderEffect(); 
    }
    #endif

    public override void FixedUpdateNetwork()
    {
        if (isPlayerFrozen) return;

        // 👇 [수정] 회복 중일 때와 아닐 때를 분리해서 계산합니다.
        if (isRestoring)
        {
            // 회복 중: 수치가 깎입니다.
            currentFreezeGauge -= restoreRate * Time.deltaTime;
        }
        else
        {
            // 밖일 때: 수치가 오릅니다.
            float increaseRate = isRapidFreezing ? 4f : 1f;
            currentFreezeGauge += increaseRate * Time.deltaTime;
        }

        currentFreezeGauge = Mathf.Clamp(currentFreezeGauge, 0f, MAX_FREEZE_GAUGE);
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);

        if (currentFreezeGauge >= MAX_FREEZE_GAUGE)
        {
            TriggerFreezeDeath();
        }
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
    public void SetRestoringState(bool state)
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