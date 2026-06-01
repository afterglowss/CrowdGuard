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

    [Header("Ground Movement")]
    [Tooltip("평지 이동용 Move GameObject (XR Rig > Locomotion > Move).\n" +
             "WalkableZone 진입 시 SetActive(true), 이탈 시 SetActive(false) 됩니다.\n" +
             "XRDebugLocomotion의 _moveObject와 동일한 오브젝트를 연결하세요.")]
    public GameObject groundMoveObject;

    [Tooltip("WalkableZone 진입 시 rig Y를 지면 높이로 보정하는 속도. 값이 클수록 빠르게 내려앉습니다.")]
    public float groundYLerpSpeed = 5f;

    [Header("Fall Settings")]
    [Tooltip("최대 추락 시간(초). 이 시간이 지나면 리스폰 페이드 시작.")]
    public float fallMaxTime = 2.5f;

    [Tooltip("리스폰 암전 페이드 아웃 시간(초).")]
    public float respawnFadeOutDuration = 0.35f;

    [Tooltip("리스폰 후 페이드 인 시간(초).")]
    public float respawnFadeInDuration = 0.8f;

    [Tooltip("경사면 충돌 시 튕김 강도. 0 = 완전 슬라이드(얼음), 1 = 완전 반사")]
    [Range(0f, 1f)]
    public float fallBounciness = 0.1f;

    // FSM 상태 인스턴스 (가비지 컬렉션 방지를 위해 미리 할당)
    public PlayerIdleState    IdleState     { get; private set; }
    public PlayerClimbingState ClimbingState { get; private set; }
    public PlayerFallingState FallingState  { get; private set; }
    public PlayerGroundState  GroundState   { get; private set; }

    public PlayerState CurrentState { get; private set; }

    /// <summary>
    /// 현재 플레이어가 안에 있는 WalkableZone. WalkableZone이 직접 설정합니다.
    /// null이면 평지 구역 밖입니다.
    /// </summary>
    public WalkableZone CurrentWalkableZone { get; set; }

    /// <summary>
    /// true일 때 ClimbingState 진입을 차단합니다.
    /// _blockClimbing이 켜진 WalkableZone에 진입하면 true, 이탈하면 false로 설정됩니다.
    /// </summary>
    public bool IsClimbingBlocked { get; set; }

    /// <summary>
    /// 텐트 입·퇴장 등 텔레포트 연출 중일 때 true.
    /// 이 동안에는 바일(IceAxe) 상태 변화가 Climbing/Falling 전환을 일으키지 못하도록 막습니다.
    /// (양손 바일을 하나씩 ForceRelease할 때, 아직 놓지 않은 다른 손이 ClimbingState를 다시
    ///  깨우고 → 이어서 FallingState로 빠지는 버그를 차단합니다.)
    /// </summary>
    public bool IsTeleporting { get; set; }

    /// <summary>로컬 머신의 PlayerController. 네트워크 RPC에서 추락 동기화에 사용됩니다.</summary>
    public static PlayerController LocalInstance { get; private set; }

    /// <summary>
    /// 상태 변경 시 발행되는 옵저버 이벤트.
    /// 멀티플레이(Photon) 스크립트는 이 이벤트를 구독하여 서버로 상태값을 날립니다.
    /// </summary>
    public event Action<PlayerState> OnStateChanged;

    private void Awake()
    {
        LocalInstance = this;
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
        // 추락 중에는 바일 상태 변화가 ClimbingState로 되돌리지 못하도록 한다.
        // (네트워크 강제 추락 시 바일이 아직 IsAttachedToWall=true인 채로
        //  이벤트가 발생하면 FallingState가 즉시 취소되는 버그 방지)
        if (CurrentState == FallingState) return;

        // 텐트 텔레포트 연출 중: 양손 바일을 하나씩 놓는 사이에 아직 박혀있는 쪽이
        // ClimbingState를 다시 깨우고 → 안전구역 밖이라 FallingState로 빠지는 것을 차단.
        // 남아있는 부착 상태를 즉시 모두 해제하고 상태 전환은 일으키지 않는다.
        if (IsTeleporting)
        {
            if (leftAxe  != null) leftAxe.IsAttachedToWall  = false;
            if (rightAxe != null) rightAxe.IsAttachedToWall = false;
            return;
        }

        // "벽에 박혀있고(Attached) AND 내 손에 쥐고있는(Held)" 바일만 유효한 등반 도구로 인정합니다.
        bool isLeftValid = leftAxe != null && leftAxe.IsAttachedToWall && leftAxe.IsHeld;
        bool isRightValid = rightAxe != null && rightAxe.IsAttachedToWall && rightAxe.IsHeld;

        // 둘 중 하나라도 유효하다면 매달리기 상태 유지
        if (isLeftValid || isRightValid)
        {
            if (IsClimbingBlocked)
            {
                // 등반 차단 구역: ClimbingState로 전환하지 않고 IsAttachedToWall을 즉시 해제합니다.
                if (leftAxe  != null) leftAxe.IsAttachedToWall  = false;
                if (rightAxe != null) rightAxe.IsAttachedToWall = false;
                return;
            }
            if (CurrentState != ClimbingState) ChangeState(ClimbingState);
        }
        else // 둘 다 놓았거나, 둘 다 벽에서 빠졌다면
        {
            if (CurrentState == ClimbingState)
            {
                // 발 위치(xrRigPivot) 대신 머리(Camera) 기준으로 거리 계산
                // 앵커는 벽 허리~가슴 높이에 있어 발 기준이면 2m 초과가 빈번함
                var safetyPos = Camera.main != null ? Camera.main.transform.position : xrRigPivot.position;

                if (!AnchorSafeZone.CheckSafety(safetyPos))
                {
                    ChangeState(FallingState);
                }
                else
                {
                    // 안전 구역 내에서 바일을 모두 놓은 경우:
                    // IsAttachedToWall을 명시적으로 해제하지 않으면 다시 잡는 순간
                    // ClimbingState.Update()가 벽에 박힌 것으로 오인해 카메라가 움직이는 버그 발생.
                    if (leftAxe  != null) leftAxe.IsAttachedToWall  = false;
                    if (rightAxe != null) rightAxe.IsAttachedToWall = false;

                    // 평지 구역 안에 있으면 GroundState로 복귀 (조이스틱 이동 유지)
                    // 구역 밖이면 기본 IdleState
                    ChangeState(CurrentWalkableZone != null ? (PlayerState)GroundState : IdleState);
                }
            }
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
        IdleState     = new PlayerIdleState(this);
        ClimbingState = new PlayerClimbingState(this);
        FallingState  = new PlayerFallingState(this);
        GroundState   = new PlayerGroundState(this);
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
    /// 네트워크 스폰 이후 GamePlayerModel에서 바일 참조를 주입받을 때 사용.
    /// OnEnable() 타이밍 문제를 우회하기 위해 별도 메서드로 분리.
    /// </summary>
    public void InjectAxes(CrowdGuard.Climbing.Tools.IceAxe.IceAxeModel left,
                           CrowdGuard.Climbing.Tools.IceAxe.IceAxeModel right)
    {
        // 기존 구독이 있다면 먼저 해제 (중복 방지)
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

        // 실제 참조 주입
        leftAxe = left;
        rightAxe = right;

        // 주입 후 즉시 이벤트 재구독
        if (leftAxe != null)
        {
            leftAxe.OnAttachedStateChanged += OnStateChangedHandler;
            leftAxe.OnHeldStateChanged += OnStateChangedHandler;
        }
        if (rightAxe != null)
        {
            rightAxe.OnAttachedStateChanged += OnStateChangedHandler;
            rightAxe.OnHeldStateChanged += OnStateChangedHandler;
        }

        Debug.Log("[PlayerController] IceAxe 참조 주입 완료.");
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
