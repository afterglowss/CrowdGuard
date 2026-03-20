using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerFallingState : PlayerState
{
    public PlayerFallingState(PlayerController player) : base(player) {}

    private float startFallY;
    private float maxFallDistance = 5.0f; // 5미터 이상 떨어지면 즉사/리스폰
    private CharacterController charController;

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Falling State: 손을 모두 놓쳐 추락합니다!");
        startFallY = player.xrRigPivot.position.y;
        charController = player.xrRigPivot.GetComponentInChildren<CharacterController>(true);
        
        // 주의: PlayerClimbingState.Exit()에서 이미 XR 물리와 중력 콜라이더가 켜졌으므로 
        // 캐릭터는 유니티 월드 물리엔진에 의해 바닥으로 리얼하게 곤두박질치기 시작합니다.
    }

    public override void Update()
    {
        // 1. 유니티 중력에 의한 실제 하강 연산을 기다립니다.
        float fallDistance = startFallY - player.xrRigPivot.position.y;

        // 2. 땅(지형)에 무사히 착지한 경우 (게임 오버 아님, 그냥 바닥에서 걸어다님)
        if (charController != null && charController.isGrounded)
        {
            if (fallDistance > 0.1f) Debug.Log("[PlayerFallingState] 땅에 무사히 착지했습니다. IdleState로 돌아갑니다.");
            player.ChangeState(player.IdleState);
            return;
        }

        // 3. 5미터 이상 끝없는 허공으로 자유낙하 했을 때 (공포 극대화 후 리스폰)
        if (fallDistance > maxFallDistance)
        {
            RespawnPlayer();
        }

        // 디버그 테스트용: 시뮬레이터에서 5미터 떨어지기 힘들 때 키보드 R버튼으로 강제 리스폰 
        if (Keyboard.current != null && Keyboard.current[Key.R].wasPressedThisFrame)
        {
            RespawnPlayer();
        }
    }

    private void RespawnPlayer()
    {
        Debug.Log("[PlayerFallingState] 아앗! 화면 암전 연출 타임. 마지막 앵커로 리스폰(텔레포트)합니다.");

        // 세이브 매니저가 있으면 그곳으로, 없으면 단순 2미터 위로
        Vector3 respawnPos = SavePointManager.Instance != null 
                             ? SavePointManager.Instance.GetRespawnPosition() 
                             : player.xrRigPivot.position + new Vector3(0, 2f, 0);

        // CharacterController가 활성화된 채로 position을 강제로 바꾸면 가끔 씹히는 충돌 버그가 있어 잠깐 끕니다.
        if (charController != null) charController.enabled = false;
        
        // 부활 위치로 이동
        player.xrRigPivot.position = respawnPos;
        
        // 물리 복구
        if (charController != null) charController.enabled = true;

        player.ChangeState(player.IdleState);
    }
}
