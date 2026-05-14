using Capstone.Photon.Game;
using SimpleAudioManager;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 추락 상태.
///
/// [물리]
/// - 단순 Y축 이동 대신 Vector3 velocity + CapsuleCast 충돌 해결.
/// - 경사면 충돌 시 hit.normal 기준으로 slide / bounce 혼합.
///   fallBounciness = 0 → 완전 슬라이드(얼음 표면)
///   fallBounciness = 1 → 완전 반사(단단한 바위)
/// - 바닥(groundLayer) 감지 없음. 인게임에서 바닥 지형 미사용이므로
///   fallMaxTime 초과 시 리스폰 시퀀스로 진입.
///
/// [리스폰 시퀀스]
///   암전 → (검은 화면에서) 텔레포트 → 밝아짐
///   텔레포트가 플레이어에게 보이지 않아 자연스러운 복귀 연출.
/// </summary>
public class PlayerFallingState : PlayerState
{
    // ── 캡슐 형태 (ClimbingState와 동일한 값 사용) ─────────────────
    private const float _capsuleBottomOffset = 0.5f;  // 허리 높이부터 체크
    private const float _capsuleTopOffset    = 1.7f;  // 머리 높이
    private const float _bodyRadius          = 0.15f;
    private const float _wallMargin          = 0.05f;

    /// <summary>
    /// RPC로 추락이 강제된 경우 true. Enter()에서 재발송을 막는 데 사용합니다.
    /// </summary>
    public static bool NetworkTriggered = false;

    // ── 상태 ───────────────────────────────────────────────────────
    private Vector3 _velocity;
    private float   _fallTimer;
    private bool    _respawnTriggered;
    private float   _escapeTimer;   // 진입 직후 벽 충돌 무시 구간

    private CharacterController      _charController;
    private Rigidbody                _rigid;
    private CapsuleCollider          _playerCapsule;   // isTrigger 캡슐 — 탈출 계산용
    private List<Behaviour>          _disabledXRScripts = new List<Behaviour>();

    public PlayerFallingState(PlayerController player) : base(player) { }

    // ── Enter / Exit ───────────────────────────────────────────────

    public override void Enter()
    {
        AudioManager.instance.PlaySFX(AudioManager.SFXType.Falling, player.xrRigPivot);

        Debug.Log("[FSM] Entered Falling State");

        _velocity         = new Vector3(0f, -3.0f, 0f); // 초기 하강 킥
        _fallTimer        = 0f;
        _respawnTriggered = false;
        _escapeTimer      = 0f; // 첫 0.25초는 벽 충돌 무시 (방금 붙어있던 벽에서 탈출)

        // 추락 중에는 평지 구역 추적을 초기화합니다.
        // 미초기화 시 리스폰 후 바일을 놓을 때 CurrentWalkableZone이 남아있어
        // 엉뚱하게 GroundState로 전환되는 버그가 발생합니다.
        player.CurrentWalkableZone = null;

        // 바일 강제 분리 — ClimbingState에서 진입 시 IsAttachedToWall이 true인 채로
        // IceAxe 이벤트가 발생하면 OnStateChangedHandler가 ClimbingState로 되돌리는
        // 버그를 방지합니다. (PlayerController.OnStateChangedHandler의 FallingState
        // 가드와 함께 동작해 두 겹으로 보호합니다.)
        if (player.leftAxe  != null) player.leftAxe.IsAttachedToWall  = false;
        if (player.rightAxe != null) player.rightAxe.IsAttachedToWall = false;

        // XR 이동 스크립트 비활성화
        _disabledXRScripts.Clear();
        foreach (var script in player.xrRigPivot.GetComponentsInChildren<Behaviour>(true))
        {
            if (script == null) continue;
            string n = script.GetType().Name;

            // ★ MoveProvider / Locomotion 계열은 비활성화하지 않는다.
            //   XR Toolkit 3.x에서 LocomotionProvider를 enabled=false 했다가
            //   enabled=true 로 되살리면 locomotionPhase 가 Moving 에 고착되어
            //   재활성화 후 전진 입력이 전혀 먹히지 않는 버그가 발생한다.
            //   대신, 실제로 위치를 적용하는 XRBodyTransformer 와
            //   CharacterControllerDriver 만 끔으로써 이동 자체를 차단한다.
            //   MoveProvider 는 계속 실행되므로 내부 Phase 가 매 프레임 정상
            //   Idle 로 돌아와 상태가 깨끗하게 유지된다.
            if ((n.Contains("XRBodyTransformer") || n.Contains("CharacterControllerDriver")) && script.enabled)
            {
                script.enabled = false;
                _disabledXRScripts.Add(script);
            }
        }

        _charController = player.xrRigPivot.GetComponentInChildren<CharacterController>(true);
        if (_charController != null) _charController.enabled = false;

        _rigid = player.xrRigPivot.GetComponentInChildren<Rigidbody>(true);
        if (_rigid != null) _rigid.isKinematic = true;

        // isTrigger CapsuleCollider 캐시 — Convex MeshCollider 탈출용
        _playerCapsule = null;
        foreach (var cap in player.xrRigPivot.GetComponentsInChildren<CapsuleCollider>(true))
        {
            if (cap.isTrigger) { _playerCapsule = cap; break; }
        }

        // 추락 비네팅 시작 (리스폰 타이밍과 분리됨)
        ScreenEffectManager.Instance?.StartFallVignette(player.fallMaxTime);

        // 네트워크에서 강제된 추락이 아닐 때만 파트너에게 전파합니다.
        if (!NetworkTriggered && PlayerManager.Instance != null)
        {
            var localModel = GamePlayerModel.LocalPlayerModel;
            if (localModel != null)
            {
                PlayerManager.Instance.RPC_TriggerPartnerFall(localModel.IsLeader
                    ? CrowdGuard.Climbing.Tools.Common.PlayerRole.Leader
                    : CrowdGuard.Climbing.Tools.Common.PlayerRole.Navigator);
            }
        }
        
        // 낙하 카운트 추가
        DataManager.Instance?.AddFallCount();

    }

    public override void Exit()
    {
        foreach (var script in _disabledXRScripts)
            if (script != null) script.enabled = true;
        _disabledXRScripts.Clear();

        if (_charController != null) _charController.enabled = true;
        if (_rigid != null) _rigid.isKinematic = false;

        player.xrRigPivot.rotation = Quaternion.Euler(0, player.xrRigPivot.eulerAngles.y, 0);
    }

    // ── Update ─────────────────────────────────────────────────────

    public override void Update()
    {
        if (player.xrRigPivot == null || _respawnTriggered) return;

        // Convex MeshCollider 내부에 들어가 있으면 먼저 밀어냄
        // (isTrigger 캡슐은 자동으로 밀려나지 않아 CapsuleCast가 내부에서 시작 → 충돌 미감지)
        if (_escapeTimer <= 0f) PushOutOfWalls();

        // 중력 누적 (3D velocity)
        _velocity += Physics.gravity * Time.deltaTime;

        // 탈출 타이머 소진
        if (_escapeTimer > 0f) _escapeTimer -= Time.deltaTime;

        // CapsuleCast로 충돌 해결 후 이동
        Vector3 move = ResolveMovement(_velocity * Time.deltaTime);
        player.xrRigPivot.position += move;

        // 카메라 눕힘 (추락 체감)
        Quaternion target = Quaternion.Euler(-60f, player.xrRigPivot.eulerAngles.y, 0);
        player.xrRigPivot.rotation = Quaternion.Lerp(
            player.xrRigPivot.rotation, target, Time.deltaTime * 5f);

        // 최대 시간 초과 → 리스폰
        _fallTimer += Time.deltaTime;
        if (_fallTimer >= player.fallMaxTime)
            TriggerRespawn();
    }

    // ── Convex MeshCollider 탈출 ───────────────────────────────────

    /// <summary>
    /// isTrigger 캡슐이 Convex MeshCollider 안에 겹쳐 있으면 바깥으로 밀어냅니다.
    /// CapsuleCast가 콜라이더 내부에서 시작하면 PhysX가 충돌을 감지하지 못하는
    /// 문제를 사전에 해결합니다.
    /// </summary>
    private void PushOutOfWalls()
    {
        if (_playerCapsule == null) return;

        Transform t      = _playerCapsule.transform;
        Vector3   center = t.TransformPoint(_playerCapsule.center);
        float     halfH  = Mathf.Max(0f, _playerCapsule.height * 0.5f - _playerCapsule.radius);
        Vector3   axis   = _playerCapsule.direction == 0 ? t.right
                         : _playerCapsule.direction == 1 ? t.up : t.forward;

        Collider[] overlaps = Physics.OverlapCapsule(
            center - axis * halfH,
            center + axis * halfH,
            _playerCapsule.radius,
            player.iceLayer,
            QueryTriggerInteraction.Ignore);

        if (overlaps.Length == 0) return;

        // ComputePenetration은 isTrigger=true 콜라이더를 지원하지 않음.
        // 계산하는 동안만 일시적으로 끄고 즉시 복원합니다.
        // Rigidbody가 없으므로 isTrigger 전환이 자동 물리 반응을 일으키지 않습니다.
        _playerCapsule.isTrigger = false;
        foreach (var col in overlaps)
        {
            if (Physics.ComputePenetration(
                    _playerCapsule, t.position, t.rotation,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 dir, out float dist))
            {
                player.xrRigPivot.position += dir * dist;
            }
        }
        _playerCapsule.isTrigger = true;
    }

    // ── 충돌 해결 ──────────────────────────────────────────────────

    private Vector3 ResolveMovement(Vector3 proposed)
    {
        if (_escapeTimer > 0f) return proposed;
        if (proposed.sqrMagnitude < 0.000001f) return proposed;

        float   dist = proposed.magnitude;
        Vector3 dir  = proposed / dist;

        Vector3 bottom = player.xrRigPivot.position + Vector3.up * _capsuleBottomOffset;
        Vector3 top    = player.xrRigPivot.position + Vector3.up * _capsuleTopOffset;

        if (!Physics.CapsuleCast(bottom, top, _bodyRadius, dir, out RaycastHit hit, dist + _wallMargin, player.iceLayer))
            return proposed;

        float allowed = Mathf.Max(0f, hit.distance - _wallMargin);

        // 속도를 슬라이드(0) ↔ 반사(1) 사이에서 혼합
        Vector3 slideVel  = Vector3.ProjectOnPlane(_velocity, hit.normal);
        Vector3 bounceVel = Vector3.Reflect(_velocity, hit.normal);
        _velocity = Vector3.Lerp(slideVel, bounceVel, player.fallBounciness);

        // 충돌면까지 이동 + 남은 이동을 hit.normal 수직 평면으로 투영 (경사면 미끄러기).
        // ProjectOnPlane이 법선 방향 성분을 제거하므로 경사면을 뚫지 않습니다.
        Vector3 remaining = proposed - dir * allowed;
        Vector3 slideMove = Vector3.ProjectOnPlane(remaining, hit.normal);
        return dir * allowed + slideMove;
    }

    // ── 리스폰 ─────────────────────────────────────────────────────

    private void TriggerRespawn()
    {
        _respawnTriggered = true;

        if (ScreenEffectManager.Instance != null)
        {
            ScreenEffectManager.Instance.StartRespawnSequence(
                player.respawnFadeOutDuration,
                onBlackScreen: DoRespawnTeleport,
                player.respawnFadeInDuration);
        }
        else
        {
            DoRespawnTeleport();
        }
    }

    /// <summary>
    /// 완전히 검은 화면에서 호출됩니다. 순간이동이 플레이어에게 보이지 않습니다.
    /// </summary>
    private void DoRespawnTeleport()
    {
        if (SavePointManager.Instance != null)
        {
            // 리더/네비게이터에 따라 리스폰 위치를 분기한다.
            // (두 플레이어가 같은 위치에 겹쳐 스폰되는 버그 방지)
            var localModel = Capstone.Photon.Game.GamePlayerModel.LocalPlayerModel;
            bool isLeader  = localModel == null || localModel.IsLeader;
            player.xrRigPivot.position = isLeader
                ? SavePointManager.Instance.GetRespawnPosition()
                : SavePointManager.Instance.GetRespawnPositionP2();
        }

        if (player.leftAxe  != null) player.leftAxe.IsAttachedToWall  = false;
        if (player.rightAxe != null) player.rightAxe.IsAttachedToWall = false;

        player.ChangeState(player.IdleState);
    }
}
