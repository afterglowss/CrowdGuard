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
        }

        private void OnDisable()
        {
            _jitterQuestAction.Disable();
        }

        private void OnDestroy()
        {
            _jitterQuestAction?.Dispose();
        }

        private void Start()
        {
            _lastRole = targetRole;
            TryFindTarget();

            if (target != null)
                _currentYaw = target.eulerAngles.y;
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

            // ── 수평 회전(Y)만 추적 ──────────────────────────────────────
            float targetYaw = target.eulerAngles.y;
            _currentYaw = Mathf.SmoothDampAngle(
                _currentYaw, targetYaw, ref _yawVelocity, rotationSmoothTime);

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
            _currentYaw = target.eulerAngles.y;
            _targetFound = true;

            Debug.Log($"[ThirdPersonFollowCamera] 타겟 찾음: {targetRole} → {target.name}");
        }
    }
}
