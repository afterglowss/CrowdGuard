using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// 기본/대기 상태. 기획상 중력·이동 등 어떤 힘도 작용하지 않습니다.
///
/// [중력 차단 이유]
/// 이 rig는 Locomotion>Gravity(GravityProvider) 오브젝트가 비활성이라
/// ContinuousMoveProvider가 m_GravityProvider == null 경로(레거시 자체 중력)로
/// 매 프레임 CharacterController를 끌어내립니다. (XRI 3.1.2 ContinuousMoveProvider 참고)
/// AnchorSafeZone에서 바일을 모두 놓으면 FallingState로는 안 가지만 이 자체 중력 때문에
/// 아래 바닥까지 가라앉는 버그가 있어, IdleState 동안 useGravity를 꺼 둡니다.
/// </summary>
public class PlayerIdleState : PlayerState
{
    private ContinuousMoveProvider[] _moveProviders;
    private bool[] _prevUseGravity;

    public PlayerIdleState(PlayerController player) : base(player) {}

    public override void Enter()
    {
        Debug.Log("[FSM] Entered Idle State: 땅에 닿아 있거나 기본 상태입니다.");

        if (_moveProviders == null)
            _moveProviders = player.xrRigPivot.GetComponentsInChildren<ContinuousMoveProvider>(true);

        if (_moveProviders.Length > 0)
        {
            if (_prevUseGravity == null || _prevUseGravity.Length != _moveProviders.Length)
                _prevUseGravity = new bool[_moveProviders.Length];

            for (int i = 0; i < _moveProviders.Length; i++)
            {
                if (_moveProviders[i] == null) continue;
                _prevUseGravity[i] = _moveProviders[i].useGravity;
                _moveProviders[i].useGravity = false;
            }
        }
    }

    public override void Exit()
    {
        if (_moveProviders == null || _prevUseGravity == null) return;

        for (int i = 0; i < _moveProviders.Length; i++)
        {
            if (_moveProviders[i] == null) continue;
            _moveProviders[i].useGravity = _prevUseGravity[i];
        }
    }

    public override void Update()
    {
        // 추후 구현: 땅에서 떨어지면 FallingState로 전환하는 로직 등
    }
}
