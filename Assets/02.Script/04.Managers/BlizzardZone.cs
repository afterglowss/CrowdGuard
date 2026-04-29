using UnityEngine;

/// <summary>
/// 눈보라 유형 1 — 상시 구간형.
/// 플레이어가 이 존 안에 있는 동안 60초마다 눈보라가 자동 발생합니다.
/// 존을 벗어나면 눈보라 사이클이 중단됩니다.
///
/// [씬 세팅]
/// 1. 빈 오브젝트에 이 컴포넌트 + Collider 추가 (Is Trigger 자동 설정)
/// 2. 눈보라 발생 구간 크기에 맞게 Collider 조정
/// 3. 인스펙터에서 interval / blizzardDuration 설정
///
/// [네트워크]
/// HazardManager.RPC_TriggerBlizzard() 를 사용하므로
/// StateAuthority만 RPC를 보내 양쪽 화면에 동시 발동.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BlizzardZone : MonoBehaviour
{
    [Tooltip("눈보라 발생 간격(초). 기획서 기준 60초.")]
    public float intervalSeconds = 60f;

    [Tooltip("눈보라 지속 시간(초). 기획서 기준 5초.")]
    public float blizzardDuration = 5f;

    [Tooltip("동결 게이지 증가 배수")]
    public float freezeMultiplier = 4f;

    // ─── 내부 상태 ──────────────────────────────────
    private Coroutine _cyclicCoroutine;
    private int _playerCount = 0; // 존 안에 있는 플레이어 수 (양쪽 클라 각자 카운트)

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerCount++;

        // 첫 번째 플레이어 진입 시 사이클 시작
        if (_playerCount == 1)
            StartCycle();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerCount = Mathf.Max(0, _playerCount - 1);

        // 모든 플레이어가 나가면 사이클 중단
        if (_playerCount == 0)
            StopCycle();
    }

    private void StartCycle()
    {
        if (_cyclicCoroutine != null) return; // 이미 실행 중
        if (HazardManager.Instance == null) return;

        _cyclicCoroutine = HazardManager.Instance.StartCyclicBlizzard(
            transform.position, intervalSeconds);

        Debug.Log($"[BlizzardZone] '{name}' 사이클 시작 ({intervalSeconds}초 간격)");
    }

    private void StopCycle()
    {
        if (_cyclicCoroutine == null) return;
        if (HazardManager.Instance == null) return;

        HazardManager.Instance.StopCoroutine(_cyclicCoroutine);
        _cyclicCoroutine = null;

        Debug.Log($"[BlizzardZone] '{name}' 사이클 중단 (플레이어 퇴장)");
    }

    private void OnDisable()
    {
        StopCycle();
        _playerCount = 0;
    }

    // ─── 씬 뷰 Gizmo ────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.6f, 1f, 0.15f);
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = new Color(0.6f, 0.6f, 1f, 0.7f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        var col = GetComponent<Collider>();
        if (col == null) return;
        UnityEditor.Handles.Label(
            col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.3f),
            $"BlizzardZone  {intervalSeconds}s마다 / {blizzardDuration}s 지속"
        );
#endif
    }
}
