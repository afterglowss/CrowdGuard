using UnityEngine;

public class PlayerFallingState : PlayerState
{
    public PlayerFallingState(PlayerController player) : base(player) {}

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Falling State: 바일을 놓쳐 추락 중입니다!");
        // 네트워크 동기화 및 빙네팅 연출 등은 여기서 Observer 이벤트를 쏴서 처리합니다.
    }

    public override void Update()
    {
        // 추후 구현: 아래로 떨어지는 물리 계산 혹은 강제 이동값 적용
        // 추후 구현: 마지막 앵커 도달 시 리스폰 처리 및 IdleState로 전환
    }
}
