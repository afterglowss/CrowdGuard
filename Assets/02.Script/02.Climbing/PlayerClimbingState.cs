using UnityEngine;
using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.IceAxe; // 팀원의 네임스페이스 추가

public class PlayerClimbingState : PlayerState
{
    public PlayerClimbingState(PlayerController player) : base(player) {}

    private CharacterController charController;
    private Rigidbody rigid;
    private List<Behaviour> disabledXRScripts = new List<Behaviour>();

    // 👇 [핵심 1] '기준 손(Active Axe)' 개념을 삭제하고, 각 손의 이전 위치를 독립적으로 기억합니다.
    private Vector3? prevLeftPos = null;
    private Vector3? prevRightPos = null;

    // 👇 [핵심 2] 안정화 딜레이도 양손 각각 따로 계산합니다!
    private float leftGraceTimer = 0f;
    private float rightGraceTimer = 0f;
    private float gracePeriod = 0.15f; 

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Climbing State: 벽에 매달렸습니다.");
        
        // 상태 진입 시 모든 추적 변수 초기화
        prevLeftPos = null;
        prevRightPos = null;
        leftGraceTimer = 0f;
        rightGraceTimer = 0f;

        disabledXRScripts.Clear();

        Behaviour[] scripts = player.xrRigPivot.GetComponentsInChildren<Behaviour>(true);
        foreach (var script in scripts)
        {
            if (script == null) continue;
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
    }

    public override void Exit()
    {
        if (charController != null) charController.enabled = true;
        if (rigid != null) rigid.isKinematic = false;

        foreach (var script in disabledXRScripts)
        {
            if (script != null) script.enabled = true;
        }
        disabledXRScripts.Clear();
    }

    public override void Update()
    {
        if (player.xrRigPivot == null) return;

        int attachedCount = 0; // 현재 딜레이가 끝나고 유효하게 힘을 받고 있는 손의 개수
        Vector3 totalDeltaLocal = Vector3.zero;

        // ---------------- [왼손 연산] ----------------
        if (player.leftAxe != null && player.leftAxe.IsAttachedToWall && player.leftAxe.IsHeld)
        {
            Transform trueHand = player.leftAxe.InteractorTransform != null ? player.leftAxe.InteractorTransform : player.leftAxe.transform;
            Vector3 currentLocal = player.xrRigPivot.InverseTransformPoint(trueHand.position);

            if (prevLeftPos == null) 
            {
                // 방금 막 벽에 박혔을 때: 위치만 기억하고 딜레이 시작
                prevLeftPos = currentLocal;
                leftGraceTimer = 0f; 
            }
            else
            {
                if (leftGraceTimer < gracePeriod)
                {
                    // 0.15초 동안은 관성을 삼켜버립니다 (이동량 누적 안 함)
                    leftGraceTimer += Time.deltaTime;
                }
                else
                {
                    // 딜레이가 끝났다면 이동량 합산!
                    totalDeltaLocal += (currentLocal - prevLeftPos.Value);
                    attachedCount++;
                }
                prevLeftPos = currentLocal; // 위치는 매 프레임 갱신
            }
        }
        else
        {
            prevLeftPos = null; // 벽에서 떨어지면 기억 삭제
        }

        // ---------------- [오른손 연산] ----------------
        if (player.rightAxe != null && player.rightAxe.IsAttachedToWall)
        {
            Transform trueHand = player.rightAxe.InteractorTransform != null ? player.rightAxe.InteractorTransform : player.rightAxe.transform;
            Vector3 currentLocal = player.xrRigPivot.InverseTransformPoint(trueHand.position);

            if (prevRightPos == null)
            {
                prevRightPos = currentLocal;
                rightGraceTimer = 0f;
            }
            else
            {
                if (rightGraceTimer < gracePeriod)
                {
                    rightGraceTimer += Time.deltaTime;
                }
                else
                {
                    totalDeltaLocal += (currentLocal - prevRightPos.Value);
                    attachedCount++;
                }
                prevRightPos = currentLocal;
            }
        }
        else
        {
            prevRightPos = null;
        }

        // ---------------- [최종 이동 적용] ----------------
        if (attachedCount == 0) return; // 둘 다 떨어졌거나, 둘 다 0.15초 딜레이 중이면 카메라 고정

        // 👇 [핵심 3] 양손이 모두 조건을 만족하면 평균을 내고( / 2), 한 손이면 그대로 사용( / 1)
        Vector3 averageDeltaLocal = totalDeltaLocal / attachedCount;
        averageDeltaLocal.z *= 0.1f;

        Vector3 deltaWorld = player.xrRigPivot.TransformDirection(averageDeltaLocal);

        // 로프 시스템 장력 체크
        if (player.ropeSystem != null)
        {
            player.ropeSystem.LimitMovement(ref deltaWorld);
        }

        // 역방향 카메라 이동
        player.xrRigPivot.position -= deltaWorld;
    }
}