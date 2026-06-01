using UnityEngine;
using Capstone.Photon.Game;

/// <summary>
/// 눈보라 구역형 — 상시 구간.
/// 로컬 플레이어가 이 존 안에 있는 동안:
///  - 시각/청각 : XR Rig의 PlayerBlizzardVisual로 1인칭 눈보라 연출 (그 플레이어 화면에만)
///  - 동결게이지 : 공유 상태이므로 StateAuthority가 집계해 가속 (네트워크 처리)
/// 존을 벗어나면 둘 다 즉시 해제됩니다.
///
/// [씬 세팅]
/// 1. 빈 오브젝트에 이 컴포넌트 + Collider 추가 (Is Trigger / Kinematic Rigidbody 자동 설정)
/// 2. 구간 크기에 맞게 Collider 조정
///    (별도 인덱스 설정 불필요 — 시각은 플레이어 Rig 하나가 담당)
///
/// [로컬/네트워크 분리]
/// - OnTriggerEnter/Exit는 "내 로컬 플레이어"일 때만 처리 (GamePlayerModel.LocalPlayerModel 비교)
/// - 시각/청각은 HazardManager.LocalBlizzardEnter/Exit() → 로컬 Rig
/// - 동결은 HazardManager.NotifyBlizzardOccupancy() → StateAuthority 집계
/// </summary>
[RequireComponent(typeof(Collider))]
public class BlizzardZone : MonoBehaviour
{
    // 로컬 플레이어가 현재 이 존 안에 있는지 (이 클라이언트 기준)
    private bool _localInside = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        // 트리거 이벤트는 두 오브젝트 중 하나 이상에 Rigidbody가 있어야 발생.
        // 플레이어엔 Rigidbody가 없으므로 정적인 이 존에 Kinematic Rigidbody를 붙인다.
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_localInside) return;          // 이미 진입 처리됨 (콜라이더 여러 개 대비)
        if (!IsLocalPlayer(other)) return; // 내 로컬 플레이어가 아니면 무시

        _localInside = true;
        ApplyEnter();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_localInside) return;
        if (!IsLocalPlayer(other)) return;

        _localInside = false;
        ApplyExit();
    }

    private void ApplyEnter()
    {
        var hm = HazardManager.Instance;
        if (hm == null) return;

        hm.LocalBlizzardZoneEnter();      // 시각/청각(Rig) + 센서 신호
        hm.NotifyBlizzardOccupancy(true);  // 동결: StateAuthority 집계

        Debug.Log($"[BlizzardZone] '{name}' 로컬 플레이어 진입");
    }

    private void ApplyExit()
    {
        var hm = HazardManager.Instance;
        if (hm == null) return;

        hm.LocalBlizzardZoneExit();
        hm.NotifyBlizzardOccupancy(false);

        Debug.Log($"[BlizzardZone] '{name}' 로컬 플레이어 퇴장");
    }

    private static bool IsLocalPlayer(Collider other)
    {
        if (!other.CompareTag("Player")) return false;
        var model = other.GetComponentInParent<GamePlayerModel>();
        return model != null && model == GamePlayerModel.LocalPlayerModel;
    }

    private void OnDisable()
    {
        // 존 비활성화 시 로컬 상태 정리
        if (_localInside) ApplyExit();
        _localInside = false;
    }

    // ─── 씬 뷰 Gizmo ────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.6f, 1f, 0.15f);
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(0.6f, 0.6f, 1f, 0.7f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        var col = GetComponent<Collider>();
        if (col == null) return;
        UnityEditor.Handles.Label(
            col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.3f),
            "BlizzardZone (구역형 상시 · 로컬 Rig)"
        );
#endif
    }
}
