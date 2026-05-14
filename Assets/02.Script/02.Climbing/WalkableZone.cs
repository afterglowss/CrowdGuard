using UnityEngine;

/// <summary>
/// 평지 구역 트리거.
/// 로컬 플레이어가 진입하면 PlayerGroundState로, 이탈하면 PlayerIdleState로 전환합니다.
///
/// [씬 설정 방법]
/// 1. 평지 지형 위에 빈 GameObject를 만들고 이 컴포넌트를 추가합니다.
/// 2. BoxCollider 등 Collider를 추가하고 Is Trigger = true 로 설정합니다.
///    (미설정 시 Awake에서 자동으로 Trigger 전환됩니다.)
/// 3. 콜라이더 크기를 평지 영역 전체를 덮도록 조정합니다.
/// 4. groundY:
///    - 기본값(false): 이 오브젝트의 Y 좌표를 바닥으로 사용합니다.
///    - useCustomGroundY = true: 직접 입력한 값을 사용합니다.
///    xrRigPivot은 발 기준이므로 바닥 Y 높이를 그대로 입력하면 됩니다.
///
/// [주의]
/// - 플레이어 XR Rig에 Rigidbody가 있어야 OnTriggerEnter/Exit 이벤트가 발생합니다.
///   (기본 XR Rig 설정에 포함되어 있습니다.)
/// - 로컬 플레이어(PlayerController.LocalInstance)만 처리하고 리모트는 무시합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WalkableZone : MonoBehaviour
{
    [Tooltip("체크하면 아래 값을 지면 Y로 사용합니다.\n" +
             "체크 해제 시 이 오브젝트의 Y 좌표를 자동으로 사용합니다.")]
    [SerializeField] private bool _useCustomGroundY = false;

    [Tooltip("플레이어 xrRigPivot이 맞춰질 Y 좌표 (발 기준 바닥 높이).")]
    [SerializeField] private float _customGroundY = 0f;

    private float GroundY => _useCustomGroundY ? _customGroundY : transform.position.y;

    // ── 초기화 ─────────────────────────────────────────────────────

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[WalkableZone] '{name}' Collider가 Trigger로 자동 설정되었습니다.", this);
        }
    }

    // ── 트리거 이벤트 ──────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        var controller = GetLocalController(other);
        if (controller == null) return;

        // 추락 중에는 평지 진입 무시 (낙하 후 착지가 아닌 자연스러운 진입만 처리)
        if (controller.CurrentState == controller.FallingState) return;

        controller.GroundState.SetGroundY(GroundY);
        controller.CurrentWalkableZone = this;

        if (controller.CurrentState != controller.GroundState)
            controller.ChangeState(controller.GroundState);
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = GetLocalController(other);
        if (controller == null) return;

        // 다른 존이 이미 currentWalkableZone을 덮어쓴 경우 무시
        if (controller.CurrentWalkableZone != this) return;

        controller.CurrentWalkableZone = null;

        // ClimbingState 중에 존을 벗어난 경우: 클라이밍은 유지하되
        // CurrentWalkableZone만 null로 처리 → 바일을 놓으면 IdleState로 복귀
        if (controller.CurrentState == controller.GroundState)
            controller.ChangeState(controller.IdleState);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    /// <summary>
    /// 콜라이더 계층에서 로컬 PlayerController를 찾아 반환합니다.
    /// 리모트 플레이어이거나 PlayerController를 찾지 못하면 null을 반환합니다.
    /// </summary>
    private static PlayerController GetLocalController(Collider other)
    {
        var controller = other.GetComponentInParent<PlayerController>();
        if (controller == null) return null;
        return controller == PlayerController.LocalInstance ? controller : null;
    }

    // ── 에디터 시각화 ──────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.18f);

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }

        // 지면 Y 라인 시각화
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color  = new Color(1f, 1f, 0f, 0.8f);
        Vector3 center = transform.position;
        center.y = GroundY;
        Gizmos.DrawLine(center + Vector3.left * 1f, center + Vector3.right * 1f);
        Gizmos.DrawLine(center + Vector3.forward * 1f, center + Vector3.back * 1f);
    }
}
