using System.Collections.Generic;
using UnityEngine;
using Capstone.Photon.Game;

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

    private CharacterController      _charController;
    private Rigidbody                _rigid;
    private List<Behaviour>          _disabledXRScripts = new List<Behaviour>();

    public PlayerFallingState(PlayerController player) : base(player) { }

    // ── Enter / Exit ───────────────────────────────────────────────

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Falling State");

        _velocity         = new Vector3(0f, -3.0f, 0f); // 초기 하강 킥
        _fallTimer        = 0f;
        _respawnTriggered = false;

        // XR 이동 스크립트 비활성화
        _disabledXRScripts.Clear();
        foreach (var script in player.xrRigPivot.GetComponentsInChildren<Behaviour>(true))
        {
            if (script == null) continue;
            string n = script.GetType().Name;
            if ((n.Contains("XRBodyTransformer") || n.Contains("CharacterControllerDriver") ||
                 n.Contains("MoveProvider")       || n.Contains("Locomotion")) && script.enabled)
            {
                script.enabled = false;
                _disabledXRScripts.Add(script);
            }
        }

        _charController = player.xrRigPivot.GetComponentInChildren<CharacterController>(true);
        if (_charController != null) _charController.enabled = false;

        _rigid = player.xrRigPivot.GetComponentInChildren<Rigidbody>(true);
        if (_rigid != null) _rigid.isKinematic = true;

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

        // 중력 누적 (3D velocity)
        _velocity += Physics.gravity * Time.deltaTime;

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

    // ── 충돌 해결 ──────────────────────────────────────────────────

    /// <summary>
    /// 제안된 이동량에 대해 CapsuleCast를 수행합니다.
    /// 충돌이 없으면 그대로 반환.
    /// 충돌이 있으면 안전 거리만큼 이동 후, 속도를 slide/bounce 혼합으로 갱신합니다.
    ///
    /// 다음 프레임부터 갱신된 velocity가 수평 성분을 가지므로
    /// 경사면을 따라 자연스럽게 흘러내리거나 튕겨나가는 효과가 생깁니다.
    /// </summary>
    private Vector3 ResolveMovement(Vector3 proposed)
    {
        if (proposed.sqrMagnitude < 0.000001f) return proposed;

        float   dist    = proposed.magnitude;
        Vector3 dir     = proposed / dist;

        Vector3 capsuleBottom = player.xrRigPivot.position + Vector3.up * _capsuleBottomOffset;
        Vector3 capsuleTop    = player.xrRigPivot.position + Vector3.up * _capsuleTopOffset;

        if (!Physics.CapsuleCast(
                capsuleBottom, capsuleTop, _bodyRadius,
                dir, out RaycastHit hit,
                dist + _wallMargin,
                player.iceLayer))
            return proposed; // 충돌 없음

        // 안전 거리 = 충돌 지점 - 여백
        float allowed = Mathf.Max(0f, hit.distance - _wallMargin);

        // 속도를 슬라이드(0) ↔ 반사(1) 사이에서 혼합
        Vector3 slideVel   = Vector3.ProjectOnPlane(_velocity, hit.normal);
        Vector3 bounceVel  = Vector3.Reflect(_velocity, hit.normal);
        _velocity = Vector3.Lerp(slideVel, bounceVel, player.fallBounciness);

        // 충돌 방향으로는 안전 거리만큼만 이동
        return dir * allowed;
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
            player.xrRigPivot.position = SavePointManager.Instance.GetRespawnPosition();

        if (player.leftAxe  != null) player.leftAxe.IsAttachedToWall  = false;
        if (player.rightAxe != null) player.rightAxe.IsAttachedToWall = false;

        player.ChangeState(player.IdleState);
    }
}
