using System;
using UnityEngine;

public class SurvivalManager : MonoBehaviour
{
    public static SurvivalManager Instance { get; private set; }

    [Header("동결 게이지 설정")]
    // 👇 인스펙터에서 편하게 드래그할 수 있도록 Range 슬라이더 추가!
    [Range(0f, 600f)] 
    public float currentFreezeGauge = 0f;
    public const float MAX_FREEZE_GAUGE = 600f;
    public const float EFFECT_START_GAUGE = 120f; // 120부터 시각 효과 시작
    
    [Tooltip("눈보라 또는 로프 패널티 시 급격한 증가를 적용할 지 여부")]
    public bool isRapidFreezing = false;

    private bool isPlayerFrozen = false; 

    public event Action<float> OnFreezeGaugeChanged;
    public event Action OnPlayerFrozen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 🚨 [핵심 추가] 인스펙터에서 값을 수정할 때마다 자동으로 불리는 유니티 마법의 함수!
    // (Play 상태가 아닐 때도 작동합니다)
    private void OnValidate()
    {
        // 인스펙터에서 드래그할 때마다 셰이더를 강제로 업데이트합니다.
        UpdateShaderEffect(); 
    }

    private void Update()
    {
        if (isPlayerFrozen) return; 

        float increaseRate = isRapidFreezing ? 4f : 1f;
        currentFreezeGauge += increaseRate * Time.deltaTime;

        currentFreezeGauge = Mathf.Clamp(currentFreezeGauge, 0f, MAX_FREEZE_GAUGE);
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);

        // 매 프레임 셰이더 업데이트
        UpdateShaderEffect();

        if (currentFreezeGauge >= MAX_FREEZE_GAUGE)
        {
            TriggerFreezeDeath();
        }
    }

    // 🚨 [핵심 추가] 셰이더 계산 로직을 밖으로 뺐습니다.
    private void UpdateShaderEffect()
    {
        float effectRange = MAX_FREEZE_GAUGE - EFFECT_START_GAUGE;
        float currentEffectValue = Mathf.Max(0f, currentFreezeGauge - EFFECT_START_GAUGE);
        
        // 0.0 ~ 1.0 사이를 절대 벗어나지 않게 Clamp01로 안전장치 추가
        float freezeRatio = Mathf.Clamp01(currentEffectValue / effectRange);

        // 글로벌 셰이더 변수 쏘기
        Shader.SetGlobalFloat("_FreezingAmount", freezeRatio);
    }

    public void SetRapidFreezing(bool isRapid)
    {
        isRapidFreezing = isRapid;
    }

    public void RestoreFreezeGauge()
    {
        currentFreezeGauge = 0f;
        isPlayerFrozen = false; 
        
        UpdateShaderEffect(); // 셰이더 0으로 초기화
        
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);
        Debug.Log("[SurvivalManager] 텐트 버너로 동결 게이지가 회복되었습니다.");
    }

    private void TriggerFreezeDeath()
    {
        isPlayerFrozen = true;
        OnPlayerFrozen?.Invoke();
        Debug.LogWarning("[SurvivalManager] 동결 게이지가 MAX에 도달하여 플레이어가 동결(추락) 상태에 빠졌습니다!");
    }
}