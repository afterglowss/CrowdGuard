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

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Climbing State: 벽에 매달렸습니다.");
        isFirstFrame = true;

        disabledXRScripts.Clear();

        // 부모 오브젝트뿐만 아니라 손자/자식 오브젝트들 깊숙한 곳에 숨어있는
        // Locomotion(이동) 관련 XR 스크립트들을 남김없이 싹 다 찾아서 잠재웁니다.
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

        // 위 스크립트들이 Move()를 호출하려 했던 실제 CharacterController도 자식까지 싹 다 뒤져서 차단
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

        Vector3 currentAxeLocalPos = GetActiveAxeLocalPosition();

        if (isFirstFrame)
        {
            previousAxeLocalPos = currentAxeLocalPos;
            isFirstFrame = false;
            return;
        }
        
        Vector3 deltaLocal = currentAxeLocalPos - previousAxeLocalPos;
        Vector3 deltaWorld = player.xrRigPivot.TransformDirection(deltaLocal);

        if (player.ropeSystem != null)
        {
            player.ropeSystem.LimitMovement(ref deltaWorld);
        }

        player.xrRigPivot.position -= deltaWorld;
        previousAxeLocalPos = currentAxeLocalPos;
    }

    private Vector3 GetActiveAxeLocalPosition()
    {
        Vector3 worldPos = Vector3.zero;

        if (player.leftAxe != null && player.leftAxe.IsAnchored) 
            worldPos = player.leftAxe.transform.position;
        else if (player.rightAxe != null && player.rightAxe.IsAnchored) 
            worldPos = player.rightAxe.transform.position;

        return player.xrRigPivot.InverseTransformPoint(worldPos);
    }
}
