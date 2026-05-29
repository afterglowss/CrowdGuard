using System.Collections;
using UnityEngine;
using Capstone.Photon.Game;
using Fusion;
using CrowdGuard.Climbing.Tools.Common;

public class TentInteriorController : NetworkBehaviour
{
    public static TentInteriorController Instance { get; private set; }

    [Header("플레이어 배치 위치")]
    [Tooltip("리더(Leader)가 앉는 위치 — 텐트 문 쪽을 바라보는 자리")]
    public Transform player1InteriorPos;
    [Tooltip("서포터(Supporter)가 앉는 위치")]
    public Transform player2InteriorPos;

    [Header("로컬 플레이어")]
    [Tooltip("씬의 XR Origin Rig 루트 오브젝트를 여기에 연결하세요.")]
    public Transform localXRRig;

    [Tooltip("텐트 보급 최소 앵커 수량")]
    [SerializeField] private int _minAnchorSupply = 5;

    [Header("랜턴 세팅 (이중 제어)")]
    public Light lanternLight;
    public GameObject lanternEmissionObj;

    // RPC 수신 측에서도 퇴장 위치를 알 수 있도록 캐시합니다.
    private Vector3 _cachedExitPos;

    // 중복 요청 방지 (양쪽 동시 버튼 클릭 등)
    private bool _isExiting = false;

    // 랜턴 현재 상태
    private bool _isLanternOn = false;

    /// <summary>
    /// 텐트 입장 시퀀스가 시작될 때 발행됩니다. (관전자 포함 전 클라이언트)
    /// ThirdPersonFollowCamera가 구독하여 추적을 일시 정지합니다.
    /// 관전자 가드보다 먼저 발행되므로 관전자 카메라에도 반드시 도달합니다.
    /// </summary>
    public static event System.Action OnTentEnter;

    /// <summary>
    /// 텐트 퇴장 시퀀스가 시작될 때 발행됩니다. (관전자 포함 전 클라이언트)
    /// ThirdPersonFollowCamera가 구독하여 추적을 재개합니다.
    /// </summary>
    public static event System.Action OnTentExit;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 이 클라이언트가 관전자(Spectator)인지 판별합니다.
    /// RPC_TentEnter / RPC_TentExit는 RpcTargets.All이라 관전자 클라이언트에도 도달하는데,
    /// 관전자는 텐트 입·퇴장 연출(화면 페이드 + 텔레포트)의 영향을 받으면 안 됩니다.
    ///   - GamePlayerSpawner의 spectatorObject 경로: GamePlayerModel이 없음 → LocalPlayerModel == null
    ///   - None 역할로 스폰된 경우: GamePlayerModel.IsSpectator == true
    /// 솔로 테스트(네트워크 미작동)에서는 관전자 개념이 없으므로 false를 반환해
    /// 기존 단독 플레이 동작(?? true 폴백)을 그대로 유지합니다.
    /// </summary>
    public bool IsLocalSpectator()
    {
        bool networkActive = PlayerManager.Instance != null &&
                             PlayerManager.Instance.Runner != null &&
                             PlayerManager.Instance.Runner.IsRunning;
        if (!networkActive) return false;

        var model = GamePlayerModel.LocalPlayerModel;
        if (model == null) return true;   // spectatorObject 경로: 플레이어 모델 자체가 없음
        return model.IsSpectator;          // None 역할로 스폰된 관전자
    }

    // ── 입장 ────────────────────────────────────────────────────────

    /// <summary>
    /// PlayerManager.RPC_TentEnter()에서 전 클라이언트에 호출됩니다.
    /// 각 클라이언트는 자신의 역할에 맞는 위치로 이동합니다.
    ///   리더    → leaderPos (pos1, 텐트 문 앞 자리)
    ///   서포터  → supporterPos (pos2)
    /// </summary>
    public void ExecuteTentEnterLocal(Vector3 leaderPos, Vector3 navigatorPos, Vector3 exitPos)
    {
        // 추적 카메라에 입장 알림 → 추적 일시정지. (관전자 가드보다 먼저 발행)
        OnTentEnter?.Invoke();

        // 관전자는 텐트 입장 연출(페이드/텔레포트)에서 제외 — 촬영 화면 보호
        if (IsLocalSpectator())
        {
            Debug.Log("[TentInteriorController] 관전자 → 텐트 입장 연출 건너뜀");
            return;
        }

        _cachedExitPos = exitPos;
        _isExiting = false; // 재입장 시 초기화

        RPC_SetLantern(false);

        AnchorBag.LocalInstance?.RefillToMinimum(_minAnchorSupply);

        // 텐트 안에서는 세이프티 로프 숨기기
        PlayerManager.Instance?.leaderSafetyRope?.SetVisible(false);

        bool isLeader = GamePlayerModel.LocalPlayerModel?.IsLeader ?? true;
        Vector3 myPos = isLeader ? leaderPos : navigatorPos;

        StartCoroutine(TransitionToInterior(myPos));
    }

    private IEnumerator TransitionToInterior(Vector3 targetPos)
    {
        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, false));

        var pc = PlayerController.LocalInstance;
        if (pc != null)
        {
            // 순서 중요: IdleState로 먼저 전환해야 ForceRelease() 시 OnStateChangedHandler가
            // ClimbingState 분기를 타지 않아 FallingState(→ 비네팅)로 빠지는 것을 방지합니다.
            pc.ChangeState(pc.IdleState);
            pc.leftAxe?.ForceRelease();   // IsAttachedToWall 해제 + 손에서 놓기
            pc.rightAxe?.ForceRelease();
        }

        // 혹시라도 잔류한 추락 비네팅을 텐트 입장 시 명시적으로 초기화합니다.
        ScreenEffectManager.Instance?.ResetVignette();

        if (localXRRig != null)
            localXRRig.position = targetPos;

        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, true));
    }

    // ── 랜턴 ────────────────────────────────────────────────────────

    public void ToggleLantern()
    {
        _isLanternOn = !_isLanternOn;
        RPC_SetLantern(_isLanternOn);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SetLantern(bool on)
    {
        // 모든 클라이언트의 _isLanternOn을 RPC 결과와 동기화합니다.
        // 이 값이 어긋나면 ToggleLantern()이 잘못된 상태를 전송하게 됩니다.
        _isLanternOn = on;

        Debug.Log($"[TentInteriorController] 랜턴 {(on ? "켜짐" : "꺼짐")}");

        if (lanternLight != null)       lanternLight.enabled = on;
        if (lanternEmissionObj != null)  lanternEmissionObj.SetActive(on);

        if (SurvivalManager.Instance != null)
            SurvivalManager.Instance.RPC_SetRestoringState(on);

        // 앵커 보급은 처음 켤 때 한 번만
        if (on)
            AnchorBag.LocalInstance?.RefillToMinimum(_minAnchorSupply);
    }

    // ── 퇴장 ────────────────────────────────────────────────────────

    /// <summary>
    /// 텐트 문(퇴장 트리거)에서 호출됩니다.
    /// 리더만 퇴장을 트리거할 수 있으며, RPC를 통해 양쪽이 동시에 퇴장합니다.
    /// </summary>
    public void ExitTent()
    {
        Debug.Log("[TentInteriorController] ExitTent() 호출됨");

        // 관전자가 텐트 문 트리거를 건드려도 퇴장을 발동시키지 못하도록 차단.
        // (관전자는 LocalPlayerModel == null이라 아래 리더 체크의 ?? true 폴백에 걸려
        //  실수로 RPC_TentExit를 보낼 수 있음)
        if (IsLocalSpectator())
        {
            Debug.Log("[TentInteriorController] 관전자는 텐트 퇴장을 트리거할 수 없습니다.");
            return;
        }

        // 네트워크 세션 중일 때만 리더 체크 (솔로 테스트에서는 항상 허용)
        bool networkActive = PlayerManager.Instance != null &&
                             PlayerManager.Instance.Runner != null &&
                             PlayerManager.Instance.Runner.IsRunning;
        if (networkActive)
        {
            bool isLeader = GamePlayerModel.LocalPlayerModel?.IsLeader ?? true;
            if (!isLeader)
            {
                Debug.Log("[TentInteriorController] 서포터는 퇴장을 트리거할 수 없습니다.");
                return;
            }
        }

        if (_isExiting) return;
        _isExiting = true;

        if (PlayerManager.Instance != null &&
            PlayerManager.Instance.Runner != null &&
            PlayerManager.Instance.Runner.IsRunning)
        {
            PlayerManager.Instance.RPC_TentExit(_cachedExitPos);
        }
        else
        {
            ExecuteTentExitLocal(_cachedExitPos);
        }
    }

    /// <summary>
    /// PlayerManager.RPC_TentExit()에서 전 클라이언트에 호출됩니다.
    /// 페이드 아웃 → 텔레포트 → 페이드 인 시퀀스를 로컬에서 실행합니다.
    /// </summary>
    public void ExecuteTentExitLocal(Vector3 exitPos)
    {
        // 추적 카메라에 퇴장 알림 → 추적 재개. (관전자 가드보다 먼저 발행)
        OnTentExit?.Invoke();

        // 관전자는 텐트 퇴장 연출(페이드/텔레포트)에서 제외 — 촬영 화면 보호.
        // 세이브 포인트/랜턴은 실제 플레이어 클라이언트의 RpcTargets.All 호출로
        // 관전자에게도 자동 동기화되므로 여기서 건너뛰어도 문제 없습니다.
        if (IsLocalSpectator())
        {
            Debug.Log("[TentInteriorController] 관전자 → 텐트 퇴장 연출 건너뜀");
            return;
        }

        RPC_SetLantern(false);

        // 세이브 포인트 갱신 (양쪽 클라이언트 모두 실행)
        if (SavePointManager.Instance != null)
            SavePointManager.Instance.ForceSetSavePoint(exitPos);

        StartCoroutine(TransitionToExterior(exitPos));
    }

    private IEnumerator TransitionToExterior(Vector3 exitPos)
    {
        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, false));

        if (localXRRig != null)
        {
            // 리더와 서포터가 겹치지 않도록 역할에 따라 반대 방향으로 배치합니다.
            bool isLeader = GamePlayerModel.LocalPlayerModel?.IsLeader ?? true;
            Vector3 offset = isLeader
                ? new Vector3(-0.5f, 0f, 0f)   // 리더: 왼쪽
                : new Vector3( 0.5f, 0f, 0f);   // 서포터: 오른쪽
            localXRRig.position = exitPos + offset;
        }

        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, true));

        // 텐트 밖으로 나왔으니 세이프티 로프 다시 표시
        PlayerManager.Instance?.leaderSafetyRope?.SetVisible(true);

        _isExiting = false; // 코루틴 완료 후 초기화 (재입장 대비)
    }
}
