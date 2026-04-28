using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 앵커가 완전 체결될 때 안전 반경을 활성화합니다.
/// 플레이어가 반경 안에 있으면 추락을 막습니다.
///
/// [설계 변경 이유]
/// 이전 방식(OnTriggerEnter/Exit 기반 static bool)은 두 가지 문제가 있었습니다.
///   1) ClimbingState 진입 시 CharacterController가 비활성화되면 Unity가
///      OnTriggerExit를 호출하지 않아 IsPlayerSafe가 true에 고착됩니다.
///   2) 멀티플레이 시 파트너의 NetworkObject 콜라이더도 트리거에 반응해
///      로컬 플레이어와 무관하게 IsPlayerSafe가 true가 됩니다.
///
/// 현재 방식: CheckSafety(localPlayerPos)를 호출 시점에 거리 계산으로 직접 판정합니다.
/// </summary>
public class AnchorSafeZone : MonoBehaviour
{
    [Tooltip("추락을 막는 안전 반경 (m). 인스펙터에서 조절하세요.")]
    public float safeRadius = 2.0f;

    // 체결이 완료된 모든 AnchorSafeZone을 추적합니다.
    private static readonly List<AnchorSafeZone> _activeZones = new List<AnchorSafeZone>();

    private bool _isSecured = false;

    private void OnEnable()
    {
        CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorController.OnAnchorSecuredGlobal += OnAnchorSecured;
    }

    private void OnDisable()
    {
        CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorController.OnAnchorSecuredGlobal -= OnAnchorSecured;
        _activeZones.Remove(this);
    }

    private void OnAnchorSecured(CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorModel model)
    {
        if (_isSecured) return;
        if (model.transform == transform || model.transform.IsChildOf(transform) || transform.IsChildOf(model.transform))
        {
            _isSecured = true;
            if (!_activeZones.Contains(this))
                _activeZones.Add(this);
        }
    }

    /// <summary>
    /// 로컬 플레이어 위치가 체결된 앵커 안전 반경 안에 있는지 직접 판정합니다.
    /// PlayerController.OnStateChangedHandler에서 추락 전환 전에 호출하세요.
    /// </summary>
    public static bool CheckSafety(Vector3 localPlayerPos)
    {
        foreach (var zone in _activeZones)
        {
            if (zone == null) continue;
            if (Vector3.Distance(localPlayerPos, zone.transform.position) <= zone.safeRadius)
                return true;
        }
        return false;
    }

    // ── Gizmo ──────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        // 미체결: 회색 점선 와이어
        if (!_isSecured)
        {
            Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, safeRadius);
            return;
        }

        // 체결됨: 초록 반투명 구체 + 와이어
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.12f);
        Gizmos.DrawSphere(transform.position, safeRadius);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, safeRadius);
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        // 체결 여부에 따라 색상 구분
        bool secured = _isSecured;
        Color zoneColor = secured ? new Color(0f, 1f, 0.5f, 0.2f) : new Color(0.6f, 0.6f, 0.6f, 0.15f);

        Gizmos.color = zoneColor;
        Gizmos.DrawSphere(transform.position, safeRadius);
        Gizmos.color = secured ? new Color(0f, 1f, 0.5f, 1f) : new Color(0.7f, 0.7f, 0.7f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, safeRadius);

        // 판정 기준점(머리)과 거리 표시 — 플레이 모드에서만
        if (Application.isPlaying && Camera.main != null)
        {
            Vector3 headPos  = Camera.main.transform.position;
            float   dist     = Vector3.Distance(headPos, transform.position);
            bool    isSafe   = secured && dist <= safeRadius;

            // 앵커 → 머리 연결선
            Gizmos.color = isSafe ? new Color(0f, 1f, 0.3f, 0.9f) : new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawLine(transform.position, headPos);

            // 머리 위치 점
            Gizmos.DrawSphere(headPos, 0.07f);

            // 거리 & 상태 레이블
            string status = isSafe ? "SAFE" : (secured ? "UNSAFE" : "미체결");
            UnityEditor.Handles.color = isSafe ? Color.green : Color.red;
            UnityEditor.Handles.Label(
                (transform.position + headPos) * 0.5f + Vector3.up * 0.15f,
                $"{status}  {dist:F2}m / {safeRadius:F1}m");
        }
        else
        {
            // 편집 모드: 반경 레이블만
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (safeRadius + 0.1f),
                $"SafeZone r={safeRadius:F1}m  {(_isSecured ? "체결됨" : "미체결")}");
        }
#endif
    }
}
