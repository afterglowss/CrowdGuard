using System;
using UnityEngine;

public class SurvivalManager : MonoBehaviour
{
    public static SurvivalManager Instance { get; private set; }

    [Header("동결 게이지 설정")]
    public float currentFreezeGauge = 0f;
    public const float MAX_FREEZE_GAUGE = 600f;
    public const float EFFECT_START_GAUGE = 120f;
    
    [Tooltip("눈보라 또는 로프 패널티 시 급격한 증가를 적용할 지 여부")]
    public bool isRapidFreezing = false;

    private bool isPlayerFrozen = false; // 사망처리 중복 방지용

    // 게이지 변동 시 뷰어(이펙트/UI)로 전달하는 이벤트 (float: 현재 게이지)
    public event Action<float> OnFreezeGaugeChanged;
    // 플레이어가 완전히 얼어 사망/추락해야 할 때 전달하는 이벤트
    public event Action OnPlayerFrozen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (isPlayerFrozen) return; // 이미 얼었으면 업데이트 중단

        // 기획 수치: 기본 초당 1, 페널티(급격히 증가) 시 초당 4 상승
        float increaseRate = isRapidFreezing ? 4f : 1f;
        currentFreezeGauge += increaseRate * Time.deltaTime;

        // 제한 (0 ~ 600)
        currentFreezeGauge = Mathf.Clamp(currentFreezeGauge, 0f, MAX_FREEZE_GAUGE);

        // 변경된 값 브로드캐스트
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);

        // 최고치 도달 시
        if (currentFreezeGauge >= MAX_FREEZE_GAUGE)
        {
            TriggerFreezeDeath();
        }
    }

    /// <summary>
    /// 외부에서(예: HazardManager, RopeSystem) 조건에 따라 동결 진행률을 4배속으로 바꿀 때 호출
    /// </summary>
    public void SetRapidFreezing(bool isRapid)
    {
        isRapidFreezing = isRapid;
    }

    /// <summary>
    /// 안전 지대(텐트 등)의 버너와 상호작용 성공 시 호출
    /// </summary>
    public void RestoreFreezeGauge()
    {
        currentFreezeGauge = 0f;
        isPlayerFrozen = false; // 리스폰 등을 위한 상태 초기화
        OnFreezeGaugeChanged?.Invoke(currentFreezeGauge);
        Debug.Log("[SurvivalManager] 텐트 버너로 동결 게이지가 회복되었습니다.");
    }

    private void TriggerFreezeDeath()
    {
        isPlayerFrozen = true;
        OnPlayerFrozen?.Invoke();
        Debug.LogWarning("[SurvivalManager] 동결 게이지가 MAX에 도달하여 플레이어가 동결(추락) 상태에 빠졌습니다!");
        // 차후 리스폰 매니저 혹은 PlayerStatus 컨트롤러가 이 이벤트를 듣고 추락/리스폰 로직을 수행하도록 연결
    }
}
