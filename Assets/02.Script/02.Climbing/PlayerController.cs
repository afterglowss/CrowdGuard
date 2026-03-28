using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("VR Settings")]
    [Tooltip("물리적인 충돌과 뷰를 제어하는 최상위 오브젝트 (XR Rig)")]
    public Transform xrRigPivot;

    [Header("Equipment")]
    public IceAxe leftAxe;
    public IceAxe rightAxe;
    public RopeSystem ropeSystem;
    private int anchoredAxeCount = 0;

    [Header("Collision Settings")]
    public LayerMask iceLayer;

    // FSM 상태 인스턴스 (가비지 컬렉션 방지를 위해 미리 할당)
    public PlayerIdleState IdleState { get; private set; }
    public PlayerClimbingState ClimbingState { get; private set; }
    public PlayerFallingState FallingState { get; private set; }

    public PlayerState CurrentState { get; private set; }

    /// <summary>
    /// 상태 변경 시 발행되는 옵저버 이벤트.
    /// 멀티플레이(Photon) 스크립트는 이 이벤트를 구독하여 서버로 상태값을 날립니다.
    /// </summary>
    public event Action<PlayerState> OnStateChanged;

    private void Awake()
    {
        InitializeStates();
    }

    private void Start()
    {
        // 시뮬레이션 시작은 Idle 상태로
        ChangeState(IdleState);
    }

    private void OnEnable()
    {
        if (leftAxe != null)
        {
            leftAxe.OnAxeHitIce += HandleAxeHit;
            leftAxe.OnAxeReleased += HandleAxeReleased;
        }
        if (rightAxe != null)
        {
            rightAxe.OnAxeHitIce += HandleAxeHit;
            rightAxe.OnAxeReleased += HandleAxeReleased;
        }
    }

    private void OnDisable()
    {
        if (leftAxe != null)
        {
            leftAxe.OnAxeHitIce -= HandleAxeHit;
            leftAxe.OnAxeReleased -= HandleAxeReleased;
        }
        if (rightAxe != null)
        {
            rightAxe.OnAxeHitIce -= HandleAxeHit;
            rightAxe.OnAxeReleased -= HandleAxeReleased;
        }
    }

    private void Update()
    {
        CurrentState?.Update();
    }

    private void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }

    private void InitializeStates()
    {
        IdleState = new PlayerIdleState(this);
        ClimbingState = new PlayerClimbingState(this);
        FallingState = new PlayerFallingState(this);
    }

    private void HandleAxeHit(IceAxe axe)
    {
        anchoredAxeCount++;
        if (CurrentState != ClimbingState)
        {
            ChangeState(ClimbingState);
        }
    }

    private void HandleAxeReleased(IceAxe axe)
    {
        anchoredAxeCount--;
        if (anchoredAxeCount < 0) anchoredAxeCount = 0;

        if (anchoredAxeCount <= 0 && CurrentState == ClimbingState)
        {
            ChangeState(FallingState);
        }
    }

    /// <summary>
    /// 상태 전환 메서드. OnStateChanged 이벤트를 자동 발동시킵니다.
    /// </summary>
    public void ChangeState(PlayerState newState)
    {
        if (CurrentState == newState) return;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();

        OnStateChanged?.Invoke(CurrentState);
    }
}
