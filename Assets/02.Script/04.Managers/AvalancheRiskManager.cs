using System.Collections;
using UnityEngine;

/// <summary>
/// 기획서: "두 플레이어 간 거리가 멀어질수록 눈사태 발생 확률이 증가하며,
/// 플레이어가 음성 채팅을 사용할 때마다 누적된 확률을 기준으로 발생 여부를 검사한다."
///
/// [Risk 증감 공식 (매초)]
///   증가: ropeStretch 비율 × maxRiskFromRope  (멀수록 빠르게 증가)
///   증가: 음성 감지 시 voiceRiskBoost 즉시 추가
///   감소: decayPerSecond 만큼 자연 감소 (항상)
///
///   → 가까이 + 조용히 = 순감소 (안전)
///   → 멀리 + 잦은 발화 = 빠른 순증가 (위험)
///
/// [직접 발동 없음]
///   이 매니저는 risk를 관리하기만 함.
///   실제 발동은 HazardTriggerZone(Avalanche)이 AccumulatedRisk를 읽어 판정.
/// </summary>
public class AvalancheRiskManager : MonoBehaviour
{
    public static AvalancheRiskManager Instance { get; private set; }

    // ─── 자연 감소 ─────────────────────────────────────────
    [Header("자연 감소")]
    [Tooltip("아무 조건 없이 초당 감소하는 위험도.\n예: 0.02 = 50초에 1.0 → 0 감소")]
    public float decayPerSecond = 0.02f;

    // ─── 로프 기반 증가 ────────────────────────────────────
    [Header("로프 팽팽함 기반 증가")]
    [Tooltip("로프가 완전히 팽팽할 때(비율 1.0) 초당 위험도 증가량.\n예: 0.05 = 20초에 1.0 도달")]
    public float maxRiskFromRope = 0.05f;

    [Tooltip("이 비율 이하의 팽팽함은 위험도 증가 없음 (안전 마진)")]
    [Range(0f, 1f)]
    public float safeStretchRatio = 0.3f;

    // ─── 음성 기반 증가 ────────────────────────────────────
    [Header("음성 기반 증가")]
    [Tooltip("말할 때마다 즉시 추가되는 위험도.\n예: 0.05 = 20번 말하면 +1.0")]
    public float voiceRiskBoost = 0.05f;

    [Tooltip("OnSpeakLoud 값이 이 이상이어야 발화로 판정 (0~100)")]
    public int voiceLoudnessThreshold = 10;

    // ─── 레퍼런스 ──────────────────────────────────────────
    [Header("레퍼런스 (비워두면 자동 탐색)")]
    public RopeSystem ropeSystem;

    // ─── 디버그 ────────────────────────────────────────────
    [Header("디버그 (읽기 전용)")]
    [Range(0f, 1f)]
    public float debugCurrentRisk;
    [Range(0f, 1f)]
    public float debugStretchRatio;

    // ───────────────────────────────────────────────────────
    private float _accumulatedRisk = 0f;

    /// <summary>HazardTriggerZone 등 외부에서 판정에 사용</summary>
    public float AccumulatedRisk => _accumulatedRisk;

    private VoiceAnalyzer _voiceAnalyzer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (ropeSystem == null)
            ropeSystem = FindObjectOfType<RopeSystem>();

        if (ropeSystem == null)
            Debug.LogWarning("[AvalancheRiskManager] RopeSystem을 찾지 못했습니다.");

        _voiceAnalyzer = FindObjectOfType<VoiceAnalyzer>();
        if (_voiceAnalyzer != null)
            _voiceAnalyzer.OnSpeakLoud += OnLocalPlayerSpoke;
        else
            Debug.LogWarning("[AvalancheRiskManager] VoiceAnalyzer를 찾지 못했습니다.");

        StartCoroutine(TickRoutine());
    }

    private void OnDestroy()
    {
        if (_voiceAnalyzer != null)
            _voiceAnalyzer.OnSpeakLoud -= OnLocalPlayerSpoke;
    }

    // ─────────────────────────────────────────────
    //  매초 Tick: 로프 증가 + 자연 감소
    // ─────────────────────────────────────────────

    private IEnumerator TickRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            float stretch = ropeSystem != null ? ropeSystem.GetStretchRatio() : 0f;
            debugStretchRatio = stretch;

            // 로프 기반 증가
            if (stretch > safeStretchRatio)
            {
                float activeRatio = Mathf.InverseLerp(safeStretchRatio, 1f, stretch);
                _accumulatedRisk += activeRatio * maxRiskFromRope;
            }

            // 자연 감소 (항상)
            _accumulatedRisk -= decayPerSecond;
            _accumulatedRisk = Mathf.Clamp01(_accumulatedRisk);

            debugCurrentRisk = _accumulatedRisk;
        }
    }

    // ─────────────────────────────────────────────
    //  음성: risk 가속만 (발동 없음)
    // ─────────────────────────────────────────────

    private void OnLocalPlayerSpoke(int loudness)
    {
        if (loudness < voiceLoudnessThreshold) return;

        _accumulatedRisk = Mathf.Clamp01(_accumulatedRisk + voiceRiskBoost);
        debugCurrentRisk = _accumulatedRisk;

        Debug.Log($"[AvalancheRiskManager] 음성 감지 (+{voiceRiskBoost:P0}) → 위험도 {_accumulatedRisk:P0}");
    }

    // ─────────────────────────────────────────────
    //  외부 공개 API
    // ─────────────────────────────────────────────

    /// <summary>
    /// HazardTriggerZone이 눈사태 발동 성공 후 위험도를 리셋할 때 호출
    /// </summary>
    public void ResetRisk(float retainRatio = 0f)
    {
        _accumulatedRisk *= retainRatio;
        _accumulatedRisk = Mathf.Clamp01(_accumulatedRisk);
        debugCurrentRisk = _accumulatedRisk;
        Debug.Log($"[AvalancheRiskManager] Risk 리셋. 잔여: {_accumulatedRisk:P0}");
    }

    // ─────────────────────────────────────────────
    //  에디터 테스트
    // ─────────────────────────────────────────────

    [ContextMenu("위험도 MAX로 설정")]
    private void SetMaxRisk()
    {
        if (!Application.isPlaying) return;
        _accumulatedRisk = 1f;
        debugCurrentRisk = 1f;
    }

    [ContextMenu("위험도 리셋")]
    private void ManualReset()
    {
        if (!Application.isPlaying) return;
        ResetRisk(0f);
    }
}
