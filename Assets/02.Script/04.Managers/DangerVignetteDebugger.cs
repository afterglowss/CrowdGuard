using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [테스트 전용] 위험 비네트(붉은 테두리) 효과를 혼자 미리 보기 위한 디버그 스크립트.
///
/// [사용법]
/// 1. 씬의 아무 오브젝트에 이 컴포넌트를 추가합니다.
/// 2. HMD를 착용한 채로 토글 키를 누르면 최대 강도 비네트가 켜집니다.
/// 3. 한 번 더 누르면 꺼집니다.
/// 4. 테스트가 끝나면 이 컴포넌트를 제거하세요.
/// </summary>
public class DangerVignetteDebugger : MonoBehaviour
{
    [Header("강도 미리 보기")]
    [Tooltip("Inspector에서 슬라이더를 움직이면 실시간으로 강도가 반영됩니다. (Play 중 전용)")]
    [Range(0f, 1f)]
    public float previewIntensity = 0f;

    [Header("키보드 토글")]
    [Tooltip("이 키를 누를 때마다 0 → 최대 → 0 순서로 전환됩니다.")]
    public KeyCode toggleKey = KeyCode.V;

    [Tooltip("토글 시 목표 강도. 1이면 완전 불투명.")]
    [Range(0f, 1f)]
    public float toggleTargetIntensity = 1.0f;

    [Header("단계별 순환 (선택)")]
    [Tooltip("체크하면 키를 누를 때마다 아래 presets를 순서대로 순환합니다.")]
    public bool cyclePresets = false;
    public float[] presets = { 0f, 0.3f, 0.6f, 1.0f };

    // ───────────────────────────────────────────────
    private const string DebugSourceId = "debug";

    private bool  _isActive      = false;
    private int   _presetIndex   = 0;
    private float _lastPreviewIntensity = -1f;

    // ───────────────────────────────────────────────

    private void OnDisable()
    {
        // 컴포넌트가 꺼지거나 제거될 때 비네트를 깔끔하게 정리
        ScreenEffectManager.Instance?.ClearDangerVignette(DebugSourceId);
    }

    private void Update()
    {
        HandleKeyToggle();
        HandlePreviewSlider();
    }

    // ── 키보드 토글 ───────────────────────────────

    private bool IsToggleKeyDown()
    {
#if ENABLE_INPUT_SYSTEM
        // New Input System
        return Keyboard.current != null &&
               Keyboard.current[Key.V].wasPressedThisFrame;
#else
        // Legacy Input System
        return Input.GetKeyDown(toggleKey);
#endif
    }

    private void HandleKeyToggle()
    {
        if (!IsToggleKeyDown()) return;

        // ── 진단 1: 키 입력은 잡혔는가? ──────────────────────────────
        var mgr = ScreenEffectManager.Instance;
        Debug.Log($"[DangerVignetteDebugger] {toggleKey} 키 감지. " +
                  $"ScreenEffectManager.Instance={(mgr != null ? "존재" : "NULL ← 문제!")}");

        if (mgr != null)
        {
            Debug.Log($"[DangerVignetteDebugger] dangerVignetteQuad={mgr.dangerVignetteQuad}, " +
                      $"Camera.main={Camera.main}");
        }

        if (cyclePresets)
        {
            _presetIndex = (_presetIndex + 1) % presets.Length;
            float target = presets[_presetIndex];
            previewIntensity = target;
            _isActive = target > 0f;

            if (_isActive)
                ScreenEffectManager.Instance?.SetDangerVignette(DebugSourceId, target);
            else
                ScreenEffectManager.Instance?.ClearDangerVignette(DebugSourceId);

            Debug.Log($"[DangerVignetteDebugger] 강도: {target:F2}  (preset {_presetIndex})");
        }
        else
        {
            _isActive = !_isActive;

            if (_isActive)
            {
                previewIntensity = toggleTargetIntensity;
                ScreenEffectManager.Instance?.SetDangerVignette(DebugSourceId, toggleTargetIntensity);
                Debug.Log($"[DangerVignetteDebugger] ON — 강도 {toggleTargetIntensity:F2}");
            }
            else
            {
                previewIntensity = 0f;
                ScreenEffectManager.Instance?.ClearDangerVignette(DebugSourceId);
                Debug.Log("[DangerVignetteDebugger] OFF");
            }
        }
    }

    [ContextMenu("전체 상태 진단 (콘솔 확인)")]
    private void DiagnoseAll()
    {
        var mgr = ScreenEffectManager.Instance;
        Debug.Log("=== DangerVignette 진단 시작 ===");
        Debug.Log($"ScreenEffectManager.Instance: {(mgr != null ? "존재" : "NULL ← ScreenEffectManager가 씬에 없거나 Awake 전")}");
        if (mgr == null) return;

        Debug.Log($"dangerVignetteQuad: {(mgr.dangerVignetteQuad != null ? mgr.dangerVignetteQuad.name : "NULL ← Quad 생성 실패 (Camera.main 문제일 가능성)")}");
        Debug.Log($"Camera.main: {(Camera.main != null ? Camera.main.name : "NULL ← XR 카메라 태그 확인 필요")}");

        if (mgr.dangerVignetteQuad != null)
        {
            var mat = mgr.dangerVignetteQuad.material;
            Debug.Log($"Material: {(mat != null ? mat.shader.name : "NULL")}");
            Debug.Log($"Material color: {(mat != null ? mat.color.ToString() : "N/A")}");
            Debug.Log($"Quad localPos: {mgr.dangerVignetteQuad.transform.localPosition}");
            Debug.Log($"Quad parent: {mgr.dangerVignetteQuad.transform.parent?.name ?? "없음"}");
        }
        Debug.Log("=== 진단 끝 ===");
    }

    // ── Inspector 슬라이더 실시간 반영 ─────────────

    private void HandlePreviewSlider()
    {
        // 슬라이더 값이 변경됐을 때만 업데이트
        if (Mathf.Approximately(previewIntensity, _lastPreviewIntensity)) return;

        _lastPreviewIntensity = previewIntensity;

        if (previewIntensity > 0f)
            ScreenEffectManager.Instance?.SetDangerVignette(DebugSourceId, previewIntensity);
        else
            ScreenEffectManager.Instance?.ClearDangerVignette(DebugSourceId);
    }

    // ── ContextMenu (Inspector 우클릭) ─────────────

    [ContextMenu("비네트 최대로 켜기 (1.0)")]
    private void SetMax()
    {
        previewIntensity = 1.0f;
        ScreenEffectManager.Instance?.SetDangerVignette(DebugSourceId, 1.0f);
        _isActive = true;
    }

    [ContextMenu("비네트 절반 (0.5)")]
    private void SetHalf()
    {
        previewIntensity = 0.5f;
        ScreenEffectManager.Instance?.SetDangerVignette(DebugSourceId, 0.5f);
        _isActive = true;
    }

    [ContextMenu("비네트 끄기")]
    private void TurnOff()
    {
        previewIntensity = 0f;
        ScreenEffectManager.Instance?.ClearDangerVignette(DebugSourceId);
        _isActive = false;
    }
}
