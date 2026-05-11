using UnityEngine;

/// <summary>
/// 평지 구역(WalkableZone) 안에 있을 때의 상태.
///
/// [역할]
/// - groundMoveObject(ContinuousMoveProvider가 달린 GameObject)를 활성화해
///   조이스틱 이동을 허용합니다.
/// - xrRigPivot.y 를 지면 높이(groundY)로 부드럽게 보정해
///   플레이어가 바닥 위에 자연스럽게 서 있는 시야를 만듭니다.
///
/// [전환 규칙]
///   WalkableZone 진입  →  이 상태로 진입
///   WalkableZone 이탈  →  IdleState
///   바일 벽에 박힘     →  ClimbingState (OnStateChangedHandler 처리)
///   추락 트리거        →  FallingState
/// </summary>
public class PlayerGroundState : PlayerState
{
    private float _targetGroundY;

    public PlayerGroundState(PlayerController player) : base(player) { }

    /// <summary>
    /// WalkableZone이 OnTriggerEnter 시 지면 Y 좌표를 주입합니다.
    /// ChangeState 전에 반드시 호출하세요.
    /// </summary>
    public void SetGroundY(float groundY) => _targetGroundY = groundY;

    // ── Enter / Exit ───────────────────────────────────────────────

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Ground State: 평지 구역 진입, 조이스틱 이동 활성화");

        if (player.groundMoveObject != null)
            player.groundMoveObject.SetActive(true);
    }

    public override void Exit()
    {
        // MoveProvider가 활성 상태(Moving Phase)에서 SetActive(false)되면
        // 내부 상태가 깔끔하게 종료된 뒤 비활성화됩니다.
        // (GameObject 전체 비활성화는 컴포넌트 enabled 토글과 달리 OnDisable이 정상 호출됨)
        if (player.groundMoveObject != null)
            player.groundMoveObject.SetActive(false);
    }

    // ── Update ─────────────────────────────────────────────────────

    public override void Update()
    {
        if (player.xrRigPivot == null) return;

        // xrRigPivot.y 를 지면 높이로 부드럽게 보정합니다.
        // VR 헤드셋의 실제 신체 높이는 그대로 반영되므로,
        // pivot 이 바닥 위에 오면 자연스럽게 서 있는 시야가 됩니다.
        Vector3 pos = player.xrRigPivot.position;
        pos.y = Mathf.Lerp(pos.y, _targetGroundY, Time.deltaTime * player.groundYLerpSpeed);
        player.xrRigPivot.position = pos;
    }
}
