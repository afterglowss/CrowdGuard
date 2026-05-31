using UnityEngine;
using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.IceAxe; // 팀원의 네임스페이스 추가

public class PlayerClimbingState : PlayerState
{
    // ── 벽 충돌 캡슐 파라미터 ──────────────────────────────────────────
    // pivot(발)에서 이 높이부터 캡슐 시작 — 발/무릎은 벽 감지에서 제외
    private const float _capsuleBottomOffset = 0.8f;
    // HMD를 읽지 못할 때 쓰는 폴백 높이
    private const float _capsuleTopFallback  = 1.7f;
    // 몸통 반경 — 좁은 틈새에서 막히면 줄이고, 관통이 생기면 키움
    private const float _bodyRadius          = 0.05f;
    // 벽과 유지할 최소 여백
    private const float _wallMargin          = 0.05f;

    // 로프 장력 비네트: 이 비율 이상 팽팽해지면 붉은 테두리 시작 / 최대
    private const float RopeVignetteStartRatio = 0.75f;
    private const float RopeVignetteMaxRatio   = 1.0f;

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

        // 벽에 매달리는 순간 평지 구역 멤버십을 무효화합니다.
        // 등반 중에는 CharacterController가 꺼져 WalkableZone.OnTriggerExit가
        // 누락될 수 있어 CurrentWalkableZone이 stale로 남고, 이후 세이프존에서
        // 바일을 모두 놓을 때 IdleState 대신 GroundState로 빠져 옛 지면 높이로
        // 가라앉는 버그가 발생합니다. 진입 시 한 번 끊어 그 경로를 차단합니다.
        //
        // [정상 케이스 안전성] 정상에서 "ClimbingState 중 WalkableZone 진입"은
        // WalkableZone.OnTriggerEnter가 CurrentWalkableZone을 다시 세팅하며
        // Climbing→Ground로 전환합니다. 그 전환은 Enter()가 아닌 Exit() 경로라
        // 이 클리어와 충돌하지 않습니다.
        player.CurrentWalkableZone = null;

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

        // 클라이밍 시작 전 한 번만 벽 관통 상태를 정리합니다.
        // CapsuleCast는 시작점이 콜라이더 내부에 있으면 충돌을 감지하지 못합니다.
        // 이미 벽 안에 있는 채로 등반을 시작하면 ClampMovementToWall이 작동하지 않으므로
        // Enter() 시점에 FallingState의 PushOutOfWalls와 동일한 방식으로 1회 탈출합니다.
        PushOutOfWallsOnce();
    }

    /// <summary>
    /// 플레이어 캡슐 콜라이더가 IceWall에 겹쳐 있으면 한 번에 밀어냅니다.
    /// Enter() 호출 시 딱 한 번만 실행 — 클라이밍 도중 매 프레임 보정은 하지 않습니다.
    /// </summary>
    private void PushOutOfWallsOnce()
    {
        CapsuleCollider capsule = null;
        foreach (var cap in player.xrRigPivot.GetComponentsInChildren<CapsuleCollider>(true))
        {
            if (cap.isTrigger) { capsule = cap; break; }
        }
        if (capsule == null) return;

        Transform t      = capsule.transform;
        Vector3   center = t.TransformPoint(capsule.center);
        float     halfH  = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
        Vector3   axis   = capsule.direction == 0 ? t.right
                         : capsule.direction == 1 ? t.up : t.forward;

        Collider[] overlaps = Physics.OverlapCapsule(
            center - axis * halfH,
            center + axis * halfH,
            capsule.radius,
            player.iceLayer,
            QueryTriggerInteraction.Ignore);

        if (overlaps.Length == 0) return;

        // ComputePenetration은 isTrigger=true 콜라이더를 지원하지 않으므로 잠깐 끔
        capsule.isTrigger = false;
        foreach (Collider col in overlaps)
        {
            if (Physics.ComputePenetration(
                    capsule, t.position, t.rotation,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 pushDir, out float dist))
            {
                player.xrRigPivot.position += pushDir * (dist + _wallMargin);
            }
        }
        capsule.isTrigger = true;
    }

    /// <summary>
    /// CapsuleCast로 벽 충돌을 감지하고, 충돌 시 벽면을 따라 슬라이드합니다.
    ///
    /// [기존 SphereCast 대비 개선점]
    ///   - Sphere → Capsule: 실제 사람 몸 형태에 맞는 충돌 검사
    ///   - 캡슐을 허리~머리(HMD 높이)에만 배치:
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
        float topOffset = Camera.main != null
            ? Camera.main.transform.position.y - origin.y
            : _capsuleTopFallback;
        Vector3 capsuleBottom = origin + Vector3.up * _capsuleBottomOffset;
        Vector3 capsuleTop    = origin + Vector3.up * topOffset;

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

        // 등반 종료 시 로프 장력 비네트 제거
        ScreenEffectManager.Instance?.ClearDangerVignette("rope");
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
        if (attachedCount == 0)
        {
            UpdateRopeVignette(); // 이동이 없어도 로프 팽팽함은 계속 표시
            return;
        }

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

        // 벽 슬라이드로 방향이 바뀐 뒤 로프 한계를 재확인
        if (player.ropeSystem != null)
        {
            player.ropeSystem.LimitMovement(ref deltaWorld);
        }

        // 역방향 카메라 이동
        player.xrRigPivot.position -= deltaWorld;

        // 로프 장력 비네트 갱신
        UpdateRopeVignette();
    }

    /// <summary>
    /// 현재 로프 팽팽함(StretchRatio)에 따라 화면 테두리 붉은 비네트를 갱신합니다.
    /// 이동 여부와 무관하게 매 프레임 호출됩니다.
    /// </summary>
    private void UpdateRopeVignette()
    {
        if (player.ropeSystem == null || ScreenEffectManager.Instance == null) return;

        float stretch   = player.ropeSystem.GetStretchRatio();
        float intensity = Mathf.InverseLerp(RopeVignetteStartRatio, RopeVignetteMaxRatio, stretch);
        ScreenEffectManager.Instance.SetDangerVignette("rope", intensity);
    }
}