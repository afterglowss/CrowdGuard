using UnityEngine;

/// <summary>
/// 눈보라 구역형 — 상시 구간.
/// 플레이어가 이 존 안에 있는 동안 파티클이 계속 재생되고 동결게이지가 빠르게 닳습니다.
/// 존을 벗어나면 즉시 꺼집니다.
///
/// [씬 세팅]
/// 1. 빈 오브젝트에 이 컴포넌트 + Collider 추가 (Is Trigger 자동 설정)
/// 2. 구간 크기에 맞게 Collider 조정
/// 3. 인스펙터에서 blizzardIndex 설정
///    HazardManager.blizzardSystems[blizzardIndex]가 실제 파티클 오브젝트입니다.
///
/// [네트워크]
/// StateAuthority만 RPC를 호출해 양쪽 화면에 동시 적용.
/// - 파티클 : 모든 클라이언트에서 재생 (RPC_ActivateBlizzardZone)
/// - 동결 패널티 : StateAuthority에서만 SurvivalManager.SetRapidFreezing() 호출
/// </summary>
[RequireComponent(typeof(Collider))]
public class BlizzardZone : MonoBehaviour
{
    [Tooltip("HazardManager.blizzardSystems 리스트의 인덱스.")]
    public int blizzardIndex = 0;

    // ─── 내부 상태 ──────────────────────────────────────────────
    private int _playerCount = 0;
    private bool _isActive   = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerCount++;

        // 첫 번째 플레이어 진입 시 즉시 활성화
        if (_playerCount == 1)
            ActivateZone();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerCount = Mathf.Max(0, _playerCount - 1);

        // 모든 플레이어가 나가면 즉시 비활성화
        if (_playerCount == 0)
            DeactivateZone();
    }

    private bool IsAuthority()
    {
        if (HazardManager.Instance == null) return true;
        var runner = HazardManager.Instance.Runner;
        if (runner == null || !runner.IsRunning) return true;
        return HazardManager.Instance.HasStateAuthority;
    }

    private void ActivateZone()
    {
        if (!IsAuthority()) return;
        if (_isActive) return;
        if (HazardManager.Instance == null) return;

        _isActive = true;
        HazardManager.Instance.RPC_ActivateBlizzardZone(blizzardIndex);
        Debug.Log($"[BlizzardZone] '{name}' 활성화 (index={blizzardIndex})");
    }

    private void DeactivateZone()
    {
        if (!IsAuthority()) return;
        if (!_isActive) return;
        if (HazardManager.Instance == null) return;

        _isActive = false;
        HazardManager.Instance.RPC_DeactivateBlizzardZone(blizzardIndex);
        Debug.Log($"[BlizzardZone] '{name}' 비활성화 (index={blizzardIndex})");
    }

    private void OnDisable()
    {
        // 오브젝트 비활성화 시 상태 정리
        if (_isActive)
            DeactivateZone();
        _playerCount = 0;
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
            $"BlizzardZone  index={blizzardIndex}  (구역형 상시)"
        );
#endif
    }
}
