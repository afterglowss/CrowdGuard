using System;
using UnityEngine;

public class SurvivalManager : MonoBehaviour
{
    public static SurvivalManager Instance { get; private set; }

    [Header("동결 게이지 설정")]
    [Range(0f, 600f)]
    public float currentFreezeGauge = 0f;
    public const float MAX_FREEZE_GAUGE = 600f;
    public const float EFFECT_START_GAUGE = 120f;

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

    private void OnValidate()
    {
        UpdateShaderEffect();
    }

    private void Update()
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

        UpdateShaderEffect();

        if (currentFreezeGauge >= MAX_FREEZE_GAUGE)
        {
            TriggerFreezeDeath();
        }
    }

    private void UpdateShaderEffect()
    {
        float effectRange = MAX_FREEZE_GAUGE - EFFECT_START_GAUGE;
        float currentEffectValue = Mathf.Max(0f, currentFreezeGauge - EFFECT_START_GAUGE);
        float freezeRatio = Mathf.Clamp01(currentEffectValue / effectRange);
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
    }

    private void TriggerFreezeDeath()
    {
        isPlayerFrozen = true;
        OnPlayerFrozen?.Invoke();
        Debug.LogWarning("[SurvivalManager] 동결!");
    }
}