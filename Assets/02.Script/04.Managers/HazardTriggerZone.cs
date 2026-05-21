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
///               useRandomDelay=true면 랜덤 대기 후 발동.
///
/// - Blizzard  : 존 진입 시 triggerProbability로 판정.
///               useRandomDelay=true면 랜덤 대기 후 발동.
///               상시 구간형은 BlizzardZone 컴포넌트 사용.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HazardTriggerZone : MonoBehaviour
{
    public enum HazardType { Avalanche, Rockfall, Blizzard }

    [Header("── 공통 설정 ──────────────────────")]
    public HazardType hazardType = HazardType.Avalanche;
    [Tooltip("HazardManager의 해당 재난 리스트 인덱스 (avalancheSystems / rockSystems / blizzardSystems)")]
    public int hazardIndex = 0;
    public bool oneShot = true;
    [Tooltip("oneShot=false일 때 쿨다운(초)")]
    public float cooldownSeconds = 60f;

    [Header("── 확률 (Rockfall / Blizzard) ────")]
    [Tooltip("Avalanche는 AvalancheRiskManager를 사용하므로 이 값 무시됨")]
    [Range(0f, 1f)]
    public float triggerProbability = 1f;

    [Header("── 랜덤 딜레이 ───────────────────")]
    [Tooltip("true면 존 진입 후 랜덤 대기 시간 뒤에 발동")]
    public bool useRandomDelay = false;
    public float minDelay = 3f;
    public float maxDelay = 10f;

    [Header("── 눈사태 전용 ───────────────────")]
    [Tooltip("판정 성공 후 위험도를 이 비율만큼 남김 (0=완전 리셋)")]
    [Range(0f, 1f)]
    public float riskRetainAfterTrigger = 0f;

    [Header("── 테스트 ───────────────────────")]
    [Tooltip("켜면 확률/위험도 판정을 건너뛰고 진입 즉시 무조건 발동합니다. 빌드 전 반드시 끄세요.")]
    public bool testMode = false;

    // ───────────────────────────────────────────
    private bool _hasFired = false;
    private float _lastFireTime = -9999f;
    private Coroutine _delayedFireCoroutine;
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
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsAuthority()) return;

        bool anyInside = AnyPlayerInside();

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
        Debug.Log($"[HazardTriggerZone] '{name}' OnPlayerEntered — oneShot={oneShot}, _hasFired={_hasFired}");

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
            Debug.Log($"[HazardTriggerZone] '{name}' 테스트 모드 — 확률 판정 스킵");

        if (useRandomDelay)
        {
            if (_delayedFireCoroutine != null) return;
            _delayedFireCoroutine = StartCoroutine(DelayedFire());
        }
        else
            Fire();
    }

    /// <summary>
    /// 네트워크 세션 중에는 스폰된 모든 플레이어의 head 위치를 확인합니다.
    /// 솔로 테스트에서는 Camera.main으로 폴백합니다.
    /// </summary>
    private bool AnyPlayerInside()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.players.Count > 0)
        {
            bool foundValidHead = false;
            foreach (var kv in PlayerManager.Instance.players)
            {
                if (kv.Value == null) continue;
                if (!kv.Value.TryGetComponent(out GamePlayerModel model)) continue;
                if (model.head == null) continue;

                foundValidHead = true;
                if (IsInsideCollider(model.head.transform.position))
                    return true;
            }
            if (foundValidHead) return false;
        }

        if (Camera.main != null)
            return IsInsideCollider(Camera.main.transform.position);

        return false;
    }

    /// <summary>
    /// bounds(AABB) 1차 → ClosestPoint 2차 순서로 판정합니다.
    /// MeshCollider(Non-Convex)는 1차만 사용합니다.
    /// </summary>
    private bool IsInsideCollider(Vector3 worldPos)
    {
        if (!_col.bounds.Contains(worldPos)) return false;
        if (_col is MeshCollider mc && !mc.convex) return true;

        Vector3 closest = _col.ClosestPoint(worldPos);
        return (closest - worldPos).sqrMagnitude < 0.001f * 0.001f;
    }

    private bool PassesProbabilityCheck()
    {
        if (hazardType == HazardType.Avalanche)
        {
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
            bool pass = Random.value < triggerProbability;
            if (!pass) Debug.Log($"[HazardTriggerZone] '{name}' 확률 판정 실패");
            return pass;
        }
    }

    private IEnumerator DelayedFire()
    {
        float delay = Random.Range(minDelay, maxDelay);
        Debug.Log($"[HazardTriggerZone] '{name}' {delay:F1}초 후 발동 예정");
        yield return new WaitForSeconds(delay);
        _delayedFireCoroutine = null;
        Fire();
    }

    private void Fire()
    {
        if (HazardManager.Instance == null)
        {
            Debug.LogWarning($"[HazardTriggerZone] '{name}' HazardManager가 씬에 없습니다!");
            return;
        }

        _hasFired = true;
        _lastFireTime = Time.time;

        switch (hazardType)
        {
            case HazardType.Avalanche:
                HazardManager.Instance.RPC_TriggerAvalanche(hazardIndex);
                AvalancheRiskManager.Instance?.ResetRisk(riskRetainAfterTrigger);
                Debug.Log($"[HazardTriggerZone] '{name}' 눈사태 발동 (index={hazardIndex})");
                break;
            case HazardType.Rockfall:
                HazardManager.Instance.RPC_TriggerRockfall(hazardIndex);
                Debug.Log($"[HazardTriggerZone] '{name}' 낙석 발동 (index={hazardIndex})");
                break;
            case HazardType.Blizzard:
                HazardManager.Instance.RPC_TriggerBlizzard(hazardIndex);
                Debug.Log($"[HazardTriggerZone] '{name}' 눈보라 발동 (index={hazardIndex})");
                break;
        }

        if (oneShot)
            _col.enabled = false;
    }

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

        string prob = hazardType == HazardType.Avalanche ? "risk%" : $"{triggerProbability * 100:F0}%";
        string delay = useRandomDelay ? $" [딜레이 {minDelay}~{maxDelay}s]" : "";
        string repeat = oneShot ? " [1회]" : $" [{cooldownSeconds}s CD]";

        string label = $"{hazardType} [index={hazardIndex}] {prob}{delay}{repeat}";
        UnityEditor.Handles.Label(col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.3f), label);
#endif
    }
}
