using UnityEngine;

/// <summary>
/// 앵커가 완전 체결될 때 안전 반경 트리거를 활성화합니다.
/// 플레이어가 반경 안에 있으면 IsPlayerSafe = true가 되어 추락을 막습니다.
///
/// XR팀 전달 사항 - PlayerController.cs OnStateChangedHandler() 수정:
///   else
///   {
///       if (CurrentState == ClimbingState && !AnchorSafeZone.IsPlayerSafe)
///           ChangeState(FallingState);
///   }
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class AnchorSafeZone : MonoBehaviour
{
    /// <summary>
    /// 플레이어가 어떤 앵커의 안전 반경 안에 있으면 true.
    /// PlayerController에서 추락 전환 전에 이 값을 체크하세요.
    /// </summary>
    public static bool IsPlayerSafe { get; private set; } = false;

    [Tooltip("추락을 막는 안전 반경 (m). 인스펙터에서 조절하세요.")]
    public float safeRadius = 2.0f;

    private SphereCollider _trigger;

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.radius = safeRadius;
        _trigger.enabled = false; // 체결 전까지 비활성
    }

    private void OnEnable()
    {
        CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorController.OnAnchorSecuredGlobal += OnAnchorSecured;
    }

    private void OnDisable()
    {
        CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorController.OnAnchorSecuredGlobal -= OnAnchorSecured;
    }

    private void OnAnchorSecured(CrowdGuard.Climbing.Tools.IceAnchor.IceAnchorModel model)
    {
        // 이 앵커 오브젝트가 체결된 경우에만 트리거 활성화
        if (model.transform == transform || model.transform.IsChildOf(transform) || transform.IsChildOf(model.transform))
        {
            _trigger.enabled = true;
            _trigger.radius = safeRadius;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            IsPlayerSafe = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            IsPlayerSafe = false;
    }

    private void OnValidate()
    {
        var col = GetComponent<SphereCollider>();
        if (col != null) col.radius = safeRadius;
    }
}
