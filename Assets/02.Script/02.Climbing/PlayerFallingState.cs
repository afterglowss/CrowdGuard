using System.Collections.Generic;
using UnityEngine;

public class PlayerFallingState : PlayerState
{
    private float verticalVelocity;
    private float fallTimer;
    private float maxFallTime = 2.5f;

    private CharacterController charController;
    private Rigidbody rigid;
    private LayerMask groundLayer; // 바닥 충돌 감지용

    private List<Behaviour> disabledXRScripts = new List<Behaviour>();

    public PlayerFallingState(PlayerController player) : base(player) 
    {
        // 바닥이나 일반 지형을 감지하기 위한 레이어 (Default 등)
        groundLayer = LayerMask.GetMask("Default", "Ground", "IceWall"); 
    }

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Falling State: 으아아아악!");
        
        // 🚨 1. 추락 딜레이 해결: 초기 하강 속도를 강제로 줘서 '훅!' 떨어지는 체감 극대화
        verticalVelocity = -3.0f; 
        fallTimer = 0f;

        disabledXRScripts.Clear();
        Behaviour[] scripts = player.xrRigPivot.GetComponentsInChildren<Behaviour>(true);
        foreach (var script in scripts)
        {
            string name = script.GetType().Name;
            if (name.Contains("XRBodyTransformer") ||
                name.Contains("CharacterControllerDriver") ||
                name.Contains("MoveProvider") ||
                name.Contains("Locomotion"))
            {
                if (script.enabled)
                {
                    script.enabled = false;
                    disabledXRScripts.Add(script);
                }
            }
        }

        charController = player.xrRigPivot.GetComponentInChildren<CharacterController>(true);
        if (charController != null) charController.enabled = false;

        rigid = player.xrRigPivot.GetComponentInChildren<Rigidbody>(true);
        if (rigid != null) rigid.isKinematic = true;

        // 시각 효과 시작 (아래 2번 항목에서 만들 스크립트 호출)
        if (ScreenEffectManager.Instance != null)
        {
            ScreenEffectManager.Instance.StartFallEffect(maxFallTime);
        }
    }

    public override void Exit()
    {
        foreach (var script in disabledXRScripts)
        {
            if (script != null) script.enabled = true;
        }
        disabledXRScripts.Clear();

        // 🚨 2. 마비 버그 해결: 상태를 나갈 때 반드시 물리 엔진을 원상복구!
        if (charController != null) charController.enabled = true;
        if (rigid != null) rigid.isKinematic = false;
        
        // 눕혀놨던 카메라 원상 복구
        player.xrRigPivot.rotation = Quaternion.Euler(0, player.xrRigPivot.eulerAngles.y, 0);
    }

    public override void Update()
    {
        if (player.xrRigPivot == null) return;

        // 가짜 중력 적용 (점점 더 빨리 떨어짐)
        verticalVelocity += Physics.gravity.y * Time.deltaTime; 
        player.xrRigPivot.position += new Vector3(0, verticalVelocity * Time.deltaTime, 0);

        // 카메라를 더 빠르게 눕힘 (딜레이 체감 개선)
        Quaternion targetRotation = Quaternion.Euler(-60f, player.xrRigPivot.eulerAngles.y, 0);
        player.xrRigPivot.rotation = Quaternion.Lerp(player.xrRigPivot.rotation, targetRotation, Time.deltaTime * 5f);

        // 🚨 3. 바닥 뚫기 해결: 떨어지는 중에 내 발밑에 바닥이 닿을 것 같으면 즉시 암전/부활!
        Vector3 rayStart = Camera.main.transform.position;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1.0f, groundLayer))
        {
            Debug.Log("[FSM] 윽! 바닥에 부딪힘. 즉시 리스폰.");
            Respawn();
            return;
        }

        fallTimer += Time.deltaTime;
        if (fallTimer >= maxFallTime)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        // 세이브 지점으로 순간이동
        player.xrRigPivot.position = SavePointManager.Instance.GetRespawnPosition();

        if (player.leftAxe != null) player.leftAxe.IsAttachedToWall = false;
        if (player.rightAxe != null) player.rightAxe.IsAttachedToWall = false;

        // 다시 Idle 상태로 (이때 Exit()가 불리면서 마비가 풀리고 카메라가 똑바로 섬)
        player.ChangeState(player.IdleState);
    }
}