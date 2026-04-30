using UnityEngine;
using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.IceAxe; // 팀원의 네임스페이스 추가

public class PlayerClimbingState : PlayerState
{
    // ── 벽 충돌 캡슐 파라미터 ──────────────────────────────────────────
    // pivot(발)에서 이 높이부터 캡슐 시작 — 발/무릎은 벽 감지에서 제외
    private const float _capsuleBottomOffset = 0.8f;
    // pivot에서 이 높이까지 캡슐 끝 — 대략 머리 높이
    private const float _capsuleTopOffset    = 1.7f;
    // 몸통 반경 — 좁은 틈새에서 막히면 줄이고, 관통이 생기면 키움
    private const float _bodyRadius          = 0.15f;
    // 벽과 유지할 최소 여백
    private const float _wallMargin          = 0.05f;

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

    /// <summary>
    /// CapsuleCast로 벽 충돌을 감지하고, 충돌 시 벽면을 따라 슬라이드합니다.
    ///
    /// [기존 SphereCast 대비 개선점]
    ///   - Sphere → Capsule: 실제 사람 몸 형태에 맞는 충돌 검사
    ///   - 캡슐을 허리~머리(_capsuleBottomOffset~_capsuleTopOffset)에만 배치:
    ///     발/무릎 높이는 제외해 경사면 오르막에서 불필요하게 막히지 않음
    ///   - 완전 정지 대신 슬라이드: 벽 법선에 수직인 성분만 남겨
    ///     기울어진 벽·오버행에서 몸이 표면을 따라 자연스럽게 이동
    ///
    /// rig는 position -= deltaWorld 로 이동하므로 실제 이동 방향 = -deltaWorld
    /// </summary>
    private Vector3 ClampMovementToWall(Vector3 deltaWorld)
    {
        if (deltaWorld.sqrMagnitude < 0.000001f) return deltaWorld;

        Vector3 origin = player.xrRigPivot.position;
        Vector3 capsuleBottom = origin + Vector3.up * _capsuleBottomOffset;
        Vector3 capsuleTop    = origin + Vector3.up * _capsuleTopOffset;

        float   moveDist = deltaWorld.magnitude;
        Vector3 moveDir  = -deltaWorld / moveDist; // rig 실제 이동 방향

        if (!Physics.CapsuleCast(
                capsuleBottom, capsuleTop, _bodyRadius,
                moveDir,
                out RaycastHit hit,
                moveDist + _wallMargin,
                player.iceLayer))
            return deltaWorld; // 충돌 없음 → 그대로

        // ── 슬라이드 계산 ──────────────────────────────────────────────
        // 의도한 이동(-deltaWorld)을 벽 법선 평면에 투영 → 벽을 따라 미끄러짐
        Vector3 intendedMove = -deltaWorld;
        Vector3 slide        = Vector3.ProjectOnPlane(intendedMove, hit.normal);

        // 슬라이드 방향에도 벽이 있는지 2차 확인
        if (slide.sqrMagnitude > 0.000001f)
        {
            float   slideDist = slide.magnitude;
            Vector3 slideDir  = slide / slideDist;

            if (Physics.CapsuleCast(
                    capsuleBottom, capsuleTop, _bodyRadius,
                    slideDir,
                    out RaycastHit slideHit,
                    slideDist + _wallMargin,
                    player.iceLayer))
            {
                float slideAllowed = Mathf.Max(0f, slideHit.distance - _wallMargin);
                slide = slideDir * slideAllowed;
            }
        }

        // caller는 position -= returnValue 이므로 slide의 부호를 반전
        return -slide;
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
        if (player.rightAxe != null && player.rightAxe.IsAttachedToWall && player.rightAxe.IsHeld)
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

        // 양손이 모두 조건을 만족하면 평균을 내고( / 2), 한 손이면 그대로 사용( / 1)
        Vector3 averageDeltaLocal = totalDeltaLocal / attachedCount;
        // z축 감쇠 제거 — 타격 튕김은 grace period(0.15s)가 이미 처리
        // 벽 관통은 아래 ClampMovementToWall()이 처리

        Vector3 deltaWorld = player.xrRigPivot.TransformDirection(averageDeltaLocal);

        // 로프 시스템 장력 체크
        if (player.ropeSystem != null)
        {
            player.ropeSystem.LimitMovement(ref deltaWorld);
        }

        // 벽 관통 방지: 실제 이동 방향으로 SphereCast해서 이동량 제한
        deltaWorld = ClampMovementToWall(deltaWorld);

        // 역방향 카메라 이동
        player.xrRigPivot.position -= deltaWorld;
    }
}