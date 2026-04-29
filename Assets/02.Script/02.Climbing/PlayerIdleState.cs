using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerController player) : base(player) {}

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Idle State: 땅에 닿아 있거나 기본 상태입니다.");
    }

    public override void Update()
    {
        // 추후 구현: 땅에서 떨어지면 FallingState로 전환하는 로직 등
    }
}
