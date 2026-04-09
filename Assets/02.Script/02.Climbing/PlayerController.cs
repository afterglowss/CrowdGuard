using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("VR Settings")]
    [Tooltip("물리적인 충돌과 뷰를 제어하는 최상위 오브젝트 (XR Rig)")]
    public Transform xrRigPivot;

    [Header("Equipment")]
    public CrowdGuard.Climbing.Tools.IceAxe.IceAxeModel leftAxe;
    public CrowdGuard.Climbing.Tools.IceAxe.IceAxeModel rightAxe;
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
            // 박히거나 빠질 때, 잡거나 놓을 때 모두 하나의 평가 함수를 호출합니다.
            leftAxe.OnAttachedStateChanged += OnStateChangedHandler;
            leftAxe.OnHeldStateChanged += OnStateChangedHandler;
        }
        if (rightAxe != null)
        {
            rightAxe.OnAttachedStateChanged += OnStateChangedHandler;
            rightAxe.OnHeldStateChanged += OnStateChangedHandler;
        }
    }

    private void OnDisable()
    {
        if (leftAxe != null)
        {
            leftAxe.OnAttachedStateChanged -= OnStateChangedHandler;
            leftAxe.OnHeldStateChanged -= OnStateChangedHandler;
        }
        if (rightAxe != null)
        {
            rightAxe.OnAttachedStateChanged -= OnStateChangedHandler;
            rightAxe.OnHeldStateChanged -= OnStateChangedHandler;
        }
    }

    private void OnStateChangedHandler(bool dummyValue)
    {
        // "벽에 박혀있고(Attached) AND 내 손에 쥐고있는(Held)" 바일만 유효한 등반 도구로 인정합니다.
        bool isLeftValid = leftAxe != null && leftAxe.IsAttachedToWall && leftAxe.IsHeld;
        bool isRightValid = rightAxe != null && rightAxe.IsAttachedToWall && rightAxe.IsHeld;

        // 둘 중 하나라도 유효하다면 매달리기 상태 유지
        if (isLeftValid || isRightValid)
        {
            if (CurrentState != ClimbingState) ChangeState(ClimbingState);
        }
        else // 둘 다 놓았거나, 둘 다 벽에서 빠졌다면 무조건 추락!
        {
            if (CurrentState == ClimbingState) ChangeState(FallingState);
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

    private void HandleAxeHit(CrowdGuard.Climbing.Tools.IceAxe.IceAxeModel axe)
    {
        anchoredAxeCount++;
        if (CurrentState != ClimbingState)
        {
            ChangeState(ClimbingState);
        }
    }

    private void HandleAxeReleased(CrowdGuard.Climbing.Tools.IceAxe.IceAxeModel axe)
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
