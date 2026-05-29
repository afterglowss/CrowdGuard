using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using Capstone.Photon.Game;
using CrowdGuard.Climbing.Tools.Common;

namespace CrowdGuard.XR
{
    /// <summary>
    /// 발표 영상 녹화용 3인칭 자동 추적 카메라.
    /// Unity Editor 플레이 모드에서 사용.
    /// PlayerManager에서 대상 역할의 body transform을 자동으로 찾아 추적합니다.
    /// </summary>
    public class ThirdPersonFollowCamera : MonoBehaviour
    {
        [Header("추적 대상")]
        [Tooltip("자동 탐색 실패 시 직접 연결 (선택)")]
        [SerializeField] private Transform target;
        [Tooltip("자동으로 추적할 역할 (기본: Leader)")]
        [SerializeField] private PlayerRole targetRole = PlayerRole.Leader;

        [Header("카메라 오프셋")]
        [Tooltip("플레이어 기준 상대 위치 (x=좌우, y=높이, z=뒤 거리)")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 1.5f, -3f);
        [Tooltip("카메라가 바라볼 높이 (body 위 n미터)")]
        [SerializeField] private float lookAtHeightOffset = 1.2f;
        [Tooltip("카메라의 수평 고정 각도 (월드 기준). 플레이어의 시야 turn에는 영향받지 않음. 0이면 월드 -Z(뒤)에서 촬영")]
        [SerializeField] private float cameraYaw = 0f;
        [Tooltip("이 키를 누르면 카메라를 현재 플레이어가 바라보는 방향의 뒤로 재정렬 (에디터 녹화용). 입력 트리거 사용 시 Active Input Handling=Both 필요")]
        [SerializeField] private KeyCode recenterKey = KeyCode.R;

        [Header("스무딩")]
        [Tooltip("위치 스무딩 시간 (클수록 느리게 따라감, 권장: 0.25 ~ 0.4)")]
        [SerializeField] private float positionSmoothTime = 0.3f;
        [Tooltip("수평 회전 스무딩 시간 (권장: 0.15 ~ 0.25)")]
        [SerializeField] private float rotationSmoothTime = 0.2f;

        [Header("카메라 지터 (촬영 연출용)")]
        [Tooltip("에디터에서 지터를 트리거할 키 (기본: Space)")]
        [SerializeField] private KeyCode editorJitterKey = KeyCode.Space;
        [Tooltip("지터 지속 시간 (초)")]
        [SerializeField] private float jitterDuration = 2.5f;
        [Tooltip("지터 최대 강도 (위치 오프셋 단위)")]
        [SerializeField] private float jitterIntensity = 0.25f;
        [Tooltip("지터 떨림 빠르기 (클수록 잘게 떨림, 권장: 15 ~ 25)")]
        [SerializeField] private float jitterFrequency = 20f;

        // Quest 왼쪽 컨트롤러 X 버튼 — Inspector 설정 없이 코드에서 직접 바인딩
        private InputAction _jitterQuestAction;

        // ── 내부 상태 ──────────────────────────────────────────────────────
        private Vector3 _posVelocity = Vector3.zero;
        private float _currentYaw;
        private float _yawVelocity;
        private bool _targetFound = false;
        private PlayerRole _lastRole;
        private float _jitterTimer = 0f;

        // 텐트 진입 중에는 추적을 멈추고 진입 직전 위치에 머뭅니다.
        private bool _isTracking = true;

        // ── Perlin 노이즈 시드 (축별로 다른 패턴) ─────────────────────────
        private const float SeedX = 0f;
        private const float SeedY = 31.7f;
        private const float SeedZ = 73.3f;

        private void Awake()
        {
            // SensorController 방식과 동일하게 바인딩
            // {PrimaryButton} = Quest 왼쪽 X 버튼
            _jitterQuestAction = new InputAction(
                "JitterTrigger",
                InputActionType.Button,
                "<XRController>{LeftHand}/{PrimaryButton}"
            );
        }

        private void OnEnable()
        {
            _jitterQuestAction.Enable();

            // 텐트 입·퇴장 시 추적 일시정지/재개
            TentInteriorController.OnTentEnter += PauseTracking;
            TentInteriorController.OnTentExit  += ResumeTracking;
        }

        private void OnDisable()
        {
            _jitterQuestAction.Disable();

            TentInteriorController.OnTentEnter -= PauseTracking;
            TentInteriorController.OnTentExit  -= ResumeTracking;
        }

        /// <summary>텐트 진입 — 현재 위치에 그대로 머물며 추적을 멈춥니다.</summary>
        private void PauseTracking()
        {
            _isTracking = false;
            Debug.Log("[ThirdPersonFollowCamera] 텐트 진입 → 추적 일시정지");
        }

        /// <summary>텐트 퇴장 — 추적을 재개합니다. SmoothDamp 속도를 초기화해 부드럽게 다시 따라잡습니다.</summary>
        private void ResumeTracking()
        {
            _isTracking = true;
            _posVelocity = Vector3.zero;
            _yawVelocity = 0f;
            Debug.Log("[ThirdPersonFollowCamera] 텐트 퇴장 → 추적 재개");
        }

        private void OnDestroy()
        {
            _jitterQuestAction?.Dispose();
        }

        private void Start()
        {
            _lastRole = targetRole;
            // 시야 turn을 따라가지 않으므로 초기 각도는 고정값(cameraYaw)으로 시작
            _currentYaw = cameraYaw;
            TryFindTarget();
        }

        private void Update()
        {
            // Inspector에서 역할이 바뀌면 타겟 초기화 후 재탐색
            if (targetRole != _lastRole)
            {
                _lastRole = targetRole;
                _targetFound = false;
                target = null;
            }

            if (!_targetFound)
                TryFindTarget();

            // 카메라를 플레이어 뒤로 재정렬 (Active Input Handling=Both 설정 후 주석 해제)
            //if (Input.GetKeyDown(recenterKey))
            //    RecenterBehindPlayer();

            // Quest X 버튼 또는 에디터 키보드로 지터 트리거
            /*if (_jitterQuestAction.WasPressedThisFrame() || Input.GetKeyDown(editorJitterKey))
                TriggerJitter();

            // 지터 타이머 감소
            if (_jitterTimer > 0f)
                _jitterTimer -= Time.deltaTime;*/
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 텐트 진입 중에는 추적을 멈추고 진입 직전 위치를 유지합니다.
            // (지터가 진행 중이면 떨림은 계속 적용되도록 아래 분기는 통과)
            if (!_isTracking)
            {
                if (_jitterTimer > 0f)
                    ApplyJitter();
                return;
            }

            // ── 수평 각도: 플레이어 시야 turn은 무시하고 고정값(cameraYaw)으로만 공전 ──
            // body의 yaw를 쓰지 않으므로 오른쪽 스틱 turn에 카메라가 휩쓸리지 않음.
            // 재정렬(RecenterBehindPlayer) 시 부드럽게 회전하도록 SmoothDampAngle은 유지.
            _currentYaw = Mathf.SmoothDampAngle(
                _currentYaw, cameraYaw, ref _yawVelocity, rotationSmoothTime);

            // ── 목표 위치: 플레이어 뒤쪽 + 위쪽 ─────────────────────────
            Quaternion yawRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            Vector3 desiredPosition = target.position + yawRotation * positionOffset;

            // ── SmoothDamp로 부드럽게 이동 ───────────────────────────────
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref _posVelocity, positionSmoothTime);

            // ── 시선: body 위 일정 높이를 바라봄 ────────────────────────
            Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeightOffset;
            transform.LookAt(lookAtPoint);

            // ── 지터 적용 ────────────────────────────────────────────────
            if (_jitterTimer > 0f)
                ApplyJitter();
        }

        /// <summary>Perlin 노이즈 기반 카메라 떨림을 현재 위치에 더합니다.</summary>
        private void ApplyJitter()
        {
            // 시간이 지날수록 약해지는 감쇠 곡선 (제곱으로 빠르게 감쇠)
            float progress = _jitterTimer / jitterDuration;
            float currentIntensity = jitterIntensity * (progress * progress);

            float t = Time.time * jitterFrequency;

            // 축별 다른 시드로 각각 독립적인 노이즈 생성
            // 좌우(X)와 앞뒤(Z)를 세게, 위아래(Y)는 절반으로 → 지진/눈사태 느낌
            Vector3 jitterOffset = new Vector3(
                (Mathf.PerlinNoise(t, SeedX) - 0.5f) * 2f,
                (Mathf.PerlinNoise(t, SeedY) - 0.5f) * 1f,
                (Mathf.PerlinNoise(t, SeedZ) - 0.5f) * 2f
            ) * currentIntensity;

            transform.position += jitterOffset;
        }

        private void TriggerJitter()
        {
            // 이미 진행 중이어도 다시 누르면 처음부터 재시작
            _jitterTimer = jitterDuration;
            Debug.Log("[ThirdPersonFollowCamera] 지터 트리거됨");
        }

        private void TryFindTarget()
        {
            if (PlayerManager.Instance == null) return;
            if (!PlayerManager.Instance.players.TryGetValue(targetRole, out NetworkObject networkObj)) return;
            if (networkObj == null) return;
            if (!networkObj.TryGetComponent<GamePlayerModel>(out GamePlayerModel model)) return;

            target = model.body.transform;
            _targetFound = true;

            Debug.Log($"[ThirdPersonFollowCamera] 타겟 찾음: {targetRole} → {target.name}");
        }

        /// <summary>
        /// 카메라 고정 각도를 현재 플레이어가 바라보는 방향의 뒤로 맞춥니다. (에디터 녹화용)
        /// 호출 후에는 다시 그 각도로 고정되어 시야 turn을 따라가지 않습니다.
        /// </summary>
        private void RecenterBehindPlayer()
        {
            if (target == null) return;
            cameraYaw = target.eulerAngles.y;
            Debug.Log("[ThirdPersonFollowCamera] 카메라를 플레이어 뒤로 재정렬");
        }
    }
}
