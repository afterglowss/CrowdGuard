using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace CrowdGuard.XR
{
    /// <summary>
    /// XR Gameplay Rig에 부착.
    /// Locomotion 하위 오브젝트들의 활성/비활성을 제어합니다.
    /// _isDebug 체크 시 스틱 자유 이동 + A/B 버튼 상승/하강 가능.
    /// </summary>
    public class XRDebugLocomotion : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("체크하면 스틱 자유 이동이 활성화됩니다 (디버깅 전용).")]
        [SerializeField] private bool _isDebug = false;
        [Tooltip("디버그 모드에서 상승/하강 속도")]
        [SerializeField] private float _debugVerticalSpeed = 2.0f;

        [Header("Debug Vertical Input (A/B 버튼)")]
        [Tooltip("상승 버튼 (예: Right Controller primaryButton / A버튼)")]
        [SerializeField] private InputActionReference _ascendAction;
        [Tooltip("하강 버튼 (예: Right Controller secondaryButton / B버튼)")]
        [SerializeField] private InputActionReference _descendAction;

        [Header("Locomotion 하위 오브젝트 연결")]
        [SerializeField] private GameObject _moveObject;
        [SerializeField] private GameObject _grabMoveObject;
        [SerializeField] private GameObject _teleportationObject;
        [SerializeField] private GameObject _climbObject;
        [SerializeField] private GameObject _gravityObject;
        [SerializeField] private GameObject _jumpObject;

        [Header("Move Provider 참조 (Move 오브젝트 내부)")]
        [Tooltip("Move 오브젝트에 달린 MoveProvider를 연결 (DynamicMoveProvider도 호환)")]
        [SerializeField] private ContinuousMoveProvider _moveProvider;

        private Transform _rigTransform;

        private void Start()
        {
            _rigTransform = transform;
            ApplyDebugState();
        }

        private void OnEnable()
        {
            if (_ascendAction != null) _ascendAction.action.Enable();
            if (_descendAction != null) _descendAction.action.Enable();
        }

        private void OnDisable()
        {
            if (_ascendAction != null) _ascendAction.action.Disable();
            if (_descendAction != null) _descendAction.action.Disable();
        }

        private void Update()
        {
            if (!_isDebug) return;

            float vertical = 0f;
            if (_ascendAction != null && _ascendAction.action.IsPressed()) vertical += 1f;
            if (_descendAction != null && _descendAction.action.IsPressed()) vertical -= 1f;

            if (vertical != 0f)
            {
                _rigTransform.position += Vector3.up * (vertical * _debugVerticalSpeed * Time.deltaTime);
            }
        }

        private void OnValidate()
        {
            ApplyDebugState();
        }

        private void ApplyDebugState()
        {
            // Move: 디버그 모드에서만 활성화 + 중력 끄기
            if (_moveObject != null) _moveObject.SetActive(_isDebug);
            if (_moveProvider != null) _moveProvider.useGravity = !_isDebug;

            // 항상 비활성화
            if (_grabMoveObject != null) _grabMoveObject.SetActive(false);
            if (_teleportationObject != null) _teleportationObject.SetActive(false);
            if (_jumpObject != null) _jumpObject.SetActive(false);

            // 추후 논의 (타 팀원 클라이밍/추락 로직과 충돌 가능)
            if (_climbObject != null) _climbObject.SetActive(false);
            if (_gravityObject != null) _gravityObject.SetActive(false);
        }
    }
}
