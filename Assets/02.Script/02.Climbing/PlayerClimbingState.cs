using UnityEngine;
using System.Collections.Generic;

public class PlayerClimbingState : PlayerState
{
    public PlayerClimbingState(PlayerController player) : base(player) {}

    private Vector3 previousAxeLocalPos;
    private bool isFirstFrame;
    private CharacterController charController;
    private Rigidbody rigid;
    private List<Behaviour> disabledXRScripts = new List<Behaviour>();

    // 👇 [추가됨] 현재 몸을 지탱하고 있는 '메인 손'을 추적합니다.
    private IceAxe activeAxe; 

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Climbing State: 벽에 매달렸습니다.");
        
        DetermineActiveAxe(); // 처음 매달릴 때 기준 손을 정합니다.
        isFirstFrame = true;

        disabledXRScripts.Clear();

        // Locomotion 관련 스크립트 비활성화 (기존 로직 동일)
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

        // 👇 [핵심 로직 변경] 
        // 1. 기준 손이 없거나, 기준 손이 방금 벽에서 떨어졌다면?
        if (activeAxe == null || !activeAxe.IsAnchored)
        {
            DetermineActiveAxe(); // 다른 손으로 기준을 교체!
            isFirstFrame = true;  // ★대참사 방지★ 델타 계산을 리셋합니다!
        }

        // 2. 바꿀 손마저 없다면 (둘 다 떨어짐) 리턴 (곧 FallingState로 넘어감)
        if (activeAxe == null) return;

        // 3. 현재 기준 손의 로컬 좌표 가져오기
        Vector3 currentAxeLocalPos = player.xrRigPivot.InverseTransformPoint(activeAxe.transform.position);

        // 4. 리셋된 첫 프레임이면 이동하지 않고 좌표만 기억합니다.
        if (isFirstFrame)
        {
            previousAxeLocalPos = currentAxeLocalPos;
            isFirstFrame = false;
            return;
        }
        
        // 5. 안전해진 델타 연산
        Vector3 deltaLocal = currentAxeLocalPos - previousAxeLocalPos;
        Vector3 deltaWorld = player.xrRigPivot.TransformDirection(deltaLocal);

        Vector3 headPosition = Camera.main.transform.position;
        float headRadius = 0.15f; // 사람 머리 크기 정도의 반경

    // 카메라 위치에서 이동하려는 방향(-deltaWorld)으로 머리 크기만 한 공을 쏴봅니다.
        if (Physics.SphereCast(headPosition, headRadius, -deltaWorld.normalized, out RaycastHit hit, deltaWorld.magnitude, player.iceLayer))
        {
            // 빙벽과 충돌했다면! 벽을 파고드는 방향의 이동량을 깎아냅니다. (벽을 따라 미끄러지도록 보정)
            deltaWorld -= hit.normal * Vector3.Dot(-deltaWorld, hit.normal);
        }

        // 보정된 최종 이동량 적용
        player.xrRigPivot.position -= deltaWorld;
        previousAxeLocalPos = currentAxeLocalPos;
    }

    // 👇 [로직 변경] 두 손 중 어느 것을 기준으로 잡을지 결정하는 함수
    private void DetermineActiveAxe()
    {
        if (player.leftAxe != null && player.leftAxe.IsAnchored)
            activeAxe = player.leftAxe;
        else if (player.rightAxe != null && player.rightAxe.IsAnchored)
            activeAxe = player.rightAxe;
        else
            activeAxe = null;
    }
}