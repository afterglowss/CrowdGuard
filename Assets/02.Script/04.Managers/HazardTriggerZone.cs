using System.Collections;
using UnityEngine;
using Capstone.Photon.Game; // GamePlayerModel, PlayerManager

/// <summary>
/// 빙산 맵 곳곳에 배치하는 재난 트리거 존.
///
/// [재난 유형별 동작]
/// - Avalanche : 존 진입 시 AvalancheRiskManager.AccumulatedRisk로 판정.
///               자체 확률 없음. 성공 시 risk 리셋.
///               (네트워크: StateAuthority만 RPC 전송 → 양측 동시 발동 보장)
///
/// - Rockfall  : 존 진입 시 triggerProbability로 판정.
///               useRandomDelay=true면 랜덤 대기 후 발동 (유형 2).
///
/// - Blizzard  : 존 진입 시 triggerProbability로 판정.
///               useRandomDelay=true면 랜덤 대기 후 발동 (유형 2).
///               상시 구간형(유형 1)은 BlizzardZone 컴포넌트 사용.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HazardTriggerZone : MonoBehaviour
{
    public enum HazardType { Avalanche, Rockfall, Blizzard }

    [Header("── 테스트 ───────────────────────────")]
    [Tooltip("켜면 확률/위험도 판정을 건너뛰고 진입 즉시 무조건 발동합니다. 빌드 전 반드시 끄세요.")]
    public bool testMode = false;

    [Header("── 공통 설정 ──────────────────────")]
    public HazardType hazardType = HazardType.Avalanche;

    [Tooltip("oneShot=false일 때 쿨다운(초)")]
    public bool oneShot = true;
    public float cooldownSeconds = 60f;

    [Header("── 랜덤 딜레이 (Rockfall/Blizzard 유형 2) ─")]
    [Tooltip("true면 존 진입 후 랜덤 대기 시간 뒤에 발동")]
    public bool useRandomDelay = false;
    public float minDelay = 3f;
    public float maxDelay = 10f;

    [Header("── Rockfall / Blizzard 자체 확률 ─────")]
    [Tooltip("Avalanche는 AvalancheRiskManager를 사용하므로 이 값 무시됨")]
    [Range(0f, 1f)]
    public float triggerProbability = 1f;

    [Header("── 눈사태 설정 ──────────────────────")]
    public int avalancheIndex = 0;
    [Tooltip("판정 성공 후 위험도를 이 비율만큼 남김 (0=완전 리셋)")]
    [Range(0f, 1f)]
    public float riskRetainAfterTrigger = 0f;

    [Header("── 낙석 설정 ───────────────────────")]
    public int rockfallPrefabIndex = 0;
    public int rockCount = 5;
    public float fallRadius = 2f;

    [Header("── 눈보라 설정 ──────────────────────")]
    public float blizzardDuration = 5f;
    public float blizzardFreezeMultiplier = 4f;

    // ───────────────────────────────────────────
    private bool _hasFired = false;
    private float _lastFireTime = -9999f;

    // 플레이어가 존 안에 있는지 추적 (폴링 방식 중복 발동 방지)
    private bool _playerWasInside = false;

    private Collider _col;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
    }

    /// <summary>
    /// Rigidbody 없이도 동작하는 위치 기반 진입 감지.
    /// StateAuthority 클라이언트만 판정하며, 양쪽 플레이어 위치를 모두 체크합니다.
    /// Collider.ClosestPoint(p) == p 이면 p는 콜라이더 내부입니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsAuthority())
        {
            // 매 프레임 찍으면 스팸이므로 1초에 한 번만
            if (Mathf.FloorToInt(Time.time) % 5 == 0 && Time.frameCount % 50 == 0)
                Debug.Log($"[HazardTriggerZone] '{name}' IsAuthority=false — 판정 스킵");
            return;
        }

        bool anyInside = AnyPlayerInside();

        // 플레이어 감지 상태를 1초에 한 번 출력 (디버그용)
        if (Time.frameCount % 50 == 0)
            Debug.Log($"[HazardTriggerZone] '{name}' ({hazardType}) AnyPlayerInside={anyInside} / _playerWasInside={_playerWasInside} / _hasFired={_hasFired}");

        if (anyInside && !_playerWasInside)
        {
            _playerWasInside = true;
            OnPlayerEntered();
        }
        else if (!anyInside)
        {
            _playerWasInside = false;
        }
    }

    private void OnPlayerEntered()
    {
        Debug.Log($"[HazardTriggerZone] '{name}' OnPlayerEntered 호출 — oneShot={oneShot}, _hasFired={_hasFired}");

        if (oneShot && _hasFired)
        {
            Debug.Log($"[HazardTriggerZone] '{name}' 이미 발동됨 (oneShot) — 스킵");
            return;
        }
        if (!oneShot && Time.time - _lastFireTime < cooldownSeconds)
        {
            Debug.Log($"[HazardTriggerZone] '{name}' 쿨다운 중 — 스킵");
            return;
        }

        if (!testMode && !PassesProbabilityCheck()) return;

        if (testMode)
            Debug.Log($"[HazardTriggerZone] '{name}' 테스트 모드 — 확률 판정 스킵, 즉시 발동");

        if (useRandomDelay)
            StartCoroutine(DelayedFire());
        else
            Fire();
    }

    /// <summary>
    /// 네트워크 세션 중에는 스폰된 모든 플레이어의 head 위치를 확인합니다.
    /// 솔로 테스트에서는 Camera.main으로 폴백합니다.
    /// </summary>
    private bool AnyPlayerInside()
    {
        // 네트워크 세션: PlayerManager가 알고 있는 모든 플레이어 체크
        if (PlayerManager.Instance != null && PlayerManager.Instance.players.Count > 0)
        {
            foreach (var kv in PlayerManager.Instance.players)
            {
                if (kv.Value == null) continue;
                if (!kv.Value.TryGetComponent(out GamePlayerModel model)) continue;
                if (model.head == null) continue;

                if (IsInsideCollider(model.head.transform.position))
                    return true;
            }
            return false;
        }

        // 솔로 테스트 폴백: Camera.main
        if (Camera.main != null)
            return IsInsideCollider(Camera.main.transform.position);

        return false;
    }

    /// <summary>
    /// 위치가 콜라이더 내부인지 판정합니다.
    /// bounds.Contains는 AABB 기반으로 모든 Primitive Collider에서 안정적으로 동작합니다.
    /// ClosestPoint 방식은 MeshCollider(Non-Convex)에서 오작동할 수 있어 사용하지 않습니다.
    /// </summary>
    private bool IsInsideCollider(Vector3 worldPos)
    {
        // 1차: AABB bounds 체크 (빠르고 안정적)
        if (!_col.bounds.Contains(worldPos))
        {
            // 5초마다 한 번씩 좌표 출력 (진단용)
            if (Mathf.FloorToInt(Time.time) % 5 == 0 && Time.frameCount % 250 == 0)
                Debug.Log($"[HazardTriggerZone] '{name}' bounds 불일치\n" +
                          $"  존 center={_col.bounds.center:F2}  size={_col.bounds.size:F2}\n" +
                          $"  플레이어 pos={worldPos:F2}");
            return false;
        }

        // 2차: ClosestPoint 정밀 체크 (BoxCollider 등 Convex 계열에서만 정확)
        // MeshCollider(Non-Convex)는 1차만으로 판정
        if (_col is MeshCollider mc && !mc.convex) return true;

        Vector3 closest = _col.ClosestPoint(worldPos);
        return (closest - worldPos).sqrMagnitude < 0.001f * 0.001f;
    }

    // ─────────────────────────────────────────────
    //  확률 판정
    // ─────────────────────────────────────────────

    private bool PassesProbabilityCheck()
    {
        if (hazardType == HazardType.Avalanche)
        {
            // 전역 누적 위험도 사용
            float risk = AvalancheRiskManager.Instance != null
                ? AvalancheRiskManager.Instance.AccumulatedRisk
                : 0f;

            if (risk <= 0f)
            {
                Debug.Log($"[HazardTriggerZone] '{name}' 눈사태 위험도 0 — 판정 스킵");
                return false;
            }

            bool pass = Random.value < risk;
            Debug.Log($"[HazardTriggerZone] '{name}' 눈사태 판정: 위험도={risk:P0} → {(pass ? "성공" : "실패")}");
            return pass;
        }
        else
        {
            // 낙석/눈보라: 자체 확률
            bool pass = Random.value < triggerProbability;
            if (!pass) Debug.Log($"[HazardTriggerZone] '{name}' 확률 판정 실패");
            return pass;
        }
    }

    // ─────────────────────────────────────────────
    //  발동
    // ─────────────────────────────────────────────

    private IEnumerator DelayedFire()
    {
        float delay = Random.Range(minDelay, maxDelay);
        Debug.Log($"[HazardTriggerZone] '{name}' {delay:F1}초 후 발동 예정");
        yield return new WaitForSeconds(delay);
        Fire();
    }

    private void Fire()
    {
        if (HazardManager.Instance == null)
        {
            Debug.LogWarning($"[HazardTriggerZone] '{name}' Fire() 호출됐지만 HazardManager가 씬에 없습니다!");
            return;
        }

        _hasFired = true;
        _lastFireTime = Time.time;

        switch (hazardType)
        {
            case HazardType.Avalanche:
                HazardManager.Instance.RPC_TriggerAvalanche(avalancheIndex);
                // 발동 성공 → 전역 위험도 리셋
                AvalancheRiskManager.Instance?.ResetRisk(riskRetainAfterTrigger);
                Debug.Log($"[HazardTriggerZone] '{name}' 눈사태 발동 (index={avalancheIndex})");
                break;

            case HazardType.Rockfall:
                HazardManager.Instance.RPC_TriggerRockfall(
                    rockfallPrefabIndex, transform.position, rockCount, fallRadius);
                Debug.Log($"[HazardTriggerZone] '{name}' 낙석 발동");
                break;

            case HazardType.Blizzard:
                HazardManager.Instance.RPC_TriggerBlizzard(
                    transform.position, blizzardDuration, blizzardFreezeMultiplier);
                Debug.Log($"[HazardTriggerZone] '{name}' 눈보라 발동");
                break;
        }

        if (oneShot)
            GetComponent<Collider>().enabled = false;
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────

    private bool IsAuthority()
    {
        if (HazardManager.Instance == null) return true;
        var runner = HazardManager.Instance.Runner;
        if (runner == null || !runner.IsRunning) return true;
        return HazardManager.Instance.HasStateAuthority;
    }

    [ContextMenu("강제 발동 (테스트)")]
    private void ForceFireInEditor()
    {
        if (!Application.isPlaying) return;
        _hasFired = false;
        _lastFireTime = -9999f;
        Fire();
    }

    // ─────────────────────────────────────────────
    //  씬 뷰 Gizmo
    // ─────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = hazardType switch
        {
            HazardType.Avalanche => new Color(0.5f, 0.8f, 1f, 0.2f),
            HazardType.Rockfall  => new Color(0.8f, 0.5f, 0.2f, 0.2f),
            HazardType.Blizzard  => new Color(0.8f, 0.8f, 1f, 0.2f),
            _                    => new Color(1f, 1f, 1f, 0.2f)
        };
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.8f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        var col = GetComponent<Collider>();
        if (col == null) return;

        string label = hazardType == HazardType.Avalanche
            ? $"Avalanche [전역 Risk 사용]{(oneShot ? " [1회]" : $" [{cooldownSeconds}s CD]")}"
            : $"{hazardType} {triggerProbability * 100:F0}%"
              + (useRandomDelay ? $" [딜레이 {minDelay}~{maxDelay}s]" : "")
              + (oneShot ? " [1회]" : $" [{cooldownSeconds}s CD]");

        UnityEditor.Handles.Label(col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.3f), label);
#endif
    }
}
