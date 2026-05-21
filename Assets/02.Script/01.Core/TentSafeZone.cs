using UnityEngine;

/// <summary>
/// 텐트 방문(퇴장) 시 해당 텐트 퇴장 위치 주변에 안전 구역을 등록합니다.
/// TentSavePoint와 같은 GameObject에 붙이세요.
///
/// [동작 원리]
/// SavePointManager.RPC_SetTentSavePoint()가 전 클라이언트에서 실행될 때
/// OnTentSavePointUpdated 이벤트가 발행됩니다.
/// TentSafeZone이 이를 구독하여 AnchorSafeZone._staticZones에 위치를 등록하고,
/// CheckSafety()가 텐트 근처에서도 추락을 막도록 합니다.
///
/// [씬에 텐트가 여러 개인 경우]
/// 각 TentSafeZone은 exteriorPos와 텐트 세이브 포인트 위치를 비교해
/// 자신의 텐트에 해당하는 이벤트만 처리합니다.
/// </summary>
public class TentSafeZone : MonoBehaviour
{
    [Tooltip("안전 반경 (m)")]
    [SerializeField] private float safeRadius = 3f;

    [Tooltip("이 텐트의 퇴장 위치 Transform. TentSavePoint.exteriorPos와 동일하게 연결하세요.")]
    [SerializeField] private Transform exteriorPos;

    [Tooltip("텐트 세이브 포인트와 exteriorPos의 허용 오차 거리 (m). 이 범위 안에서만 등록됩니다.")]
    [SerializeField] private float matchTolerance = 1f;

    private bool _isActivated = false;

    private void Awake()
    {
        // exteriorPos가 Inspector에서 연결되지 않은 경우,
        // 같은 GameObject의 TentSavePoint에서 자동으로 가져옵니다.
        if (exteriorPos == null)
        {
            var tentSavePoint = GetComponent<TentSavePoint>();
            if (tentSavePoint != null)
            {
                exteriorPos = tentSavePoint.exteriorPos;
                Debug.Log($"[TentSafeZone] exteriorPos를 TentSavePoint에서 자동 연결: {exteriorPos?.name}");
            }
        }
    }

    private void OnEnable()
    {
        SavePointManager.OnTentSavePointUpdated += OnTentSavePointUpdated;
    }

    private void OnDisable()
    {
        SavePointManager.OnTentSavePointUpdated -= OnTentSavePointUpdated;
    }

    private void OnTentSavePointUpdated(Vector3 tentPos)
    {
        if (_isActivated) return;

        // 이 텐트의 퇴장 위치와 일치할 때만 처리
        // (씬에 텐트가 여러 개일 경우 각자 자신의 위치만 담당)
        Vector3 myPos = exteriorPos != null ? exteriorPos.position : transform.position;
        if (Vector3.Distance(tentPos, myPos) > matchTolerance) return;

        AnchorSafeZone.RegisterStaticZone(myPos, safeRadius);
        _isActivated = true;

        Debug.Log($"[TentSafeZone] '{gameObject.name}' 안전 구역 활성화. 위치: {myPos}, 반경: {safeRadius}m");
    }

    // ── Gizmo ──────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 center = exteriorPos != null ? exteriorPos.position : transform.position;

        if (!_isActivated)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.1f);
            Gizmos.DrawWireSphere(center, safeRadius);
            return;
        }

        // 활성화됨: 파란 반투명 구체 + 와이어
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.12f);
        Gizmos.DrawSphere(center, safeRadius);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireSphere(center, safeRadius);
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        Vector3 center = exteriorPos != null ? exteriorPos.position : transform.position;

        Gizmos.color = _isActivated
            ? new Color(0.2f, 0.6f, 1f, 0.2f)
            : new Color(0.5f, 0.5f, 0.5f, 0.15f);
        Gizmos.DrawSphere(center, safeRadius);

        Gizmos.color = _isActivated
            ? new Color(0.2f, 0.6f, 1f, 1f)
            : new Color(0.6f, 0.6f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(center, safeRadius);

        string status = _isActivated ? "활성화됨" : "미활성 (텐트 미방문)";
        UnityEditor.Handles.color = _isActivated ? new Color(0.2f, 0.6f, 1f) : Color.gray;
        UnityEditor.Handles.Label(
            center + Vector3.up * (safeRadius + 0.1f),
            $"TentSafeZone  r={safeRadius:F1}m  [{status}]");
#endif
    }
}
