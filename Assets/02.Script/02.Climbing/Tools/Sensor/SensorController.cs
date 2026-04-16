using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
// namespace MSEX 가정 (HazardManager 소속)

namespace MSEX.Climbing.Tools
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class SensorController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private InputActionReference thumbstickAction;

        public SensorMode CurrentMode { get; private set; } = SensorMode.Avalanche;

        public event Action<SensorMode> OnModeChanged;
        public event Action<HazardData> OnHazardDetected; // 추적 시작 이벤트

        public float CurrentIntensity { get; private set; }
        private HazardData activeHazard;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable;
        private bool isHeld = false;
        private bool thumbstickAxisInUse = false;

        private void Awake()
        {
            interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        private void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);

            // D-04: HazardManager 이벤트 구독 (안전 호출을 위해 일단 null 체크 없이 하거나 Instance 체크)
            try {
                HazardManager.OnHazardWarning += HandleHazardWarning;
            } catch { } // Tests may not instantiate HazardManager immediately
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);

            try {
                HazardManager.OnHazardWarning -= HandleHazardWarning;
            } catch { }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            isHeld = true;
            if (thumbstickAction != null && thumbstickAction.action != null)
                thumbstickAction.action.Enable();
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            isHeld = false;
        }

        private void Update()
        {
            // 스틱을 왼쪽/오른쪽으로 강하게 밀었을 때 모드 순환
            if (isHeld && thumbstickAction != null && thumbstickAction.action != null)
            {
                Vector2 stickValue = thumbstickAction.action.ReadValue<Vector2>();

                if (Mathf.Abs(stickValue.x) > 0.7f)
                {
                    if (!thumbstickAxisInUse)
                    {
                        thumbstickAxisInUse = true;
                        CycleMode(stickValue.x > 0 ? 1 : -1);
                    }
                }
                else
                {
                    thumbstickAxisInUse = false;
                }
            }

            // 실시간 신호 강도 추적
            if (activeHazard != null)
            {
                Vector3 diff = activeHazard.Location - transform.position;
                diff.y = 0; 
                diff.Normalize();
                
                float dot = Vector3.Dot(transform.forward, diff);
                CurrentIntensity = Mathf.Clamp01((dot + 1f) / 2f);
            }
        }

        /// <summary>
        /// 테스트용 및 외부 호출용 수동 모드 변경
        /// </summary>
        public void MockToggleMode()
        {
            CycleMode(1);
        }

        private void CycleMode(int direction)
        {
            int modeCount = Enum.GetNames(typeof(SensorMode)).Length;
            int nextModeIndex = ((int)CurrentMode + direction + modeCount) % modeCount;
            CurrentMode = (SensorMode)nextModeIndex;

            Debug.Log($"[SensorController] Mode switched: {CurrentMode}");
            OnModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// 테스트 코드에서 외부 주입으로 이벤트를 검증하기 위해 public 처리
        /// </summary>
        public void HandleHazardWarning(HazardData data)
        {
            // 1. 현재 모드에 맞는 페이로드인지 확인
            bool match = false;
            if (CurrentMode == SensorMode.Avalanche && data is AvalancheData) match = true;
            if (CurrentMode == SensorMode.Rockfall && data is RockfallData) match = true;
            if (CurrentMode == SensorMode.Blizzard && data is BlizzardData) match = true;

            if (!match) return;

            // 추적 시작 설정
            activeHazard = data;
            CancelInvoke(nameof(ClearActiveHazard));
            Invoke(nameof(ClearActiveHazard), 15f); // 15초간 추적

            OnHazardDetected?.Invoke(data);
            Debug.Log($"[SensorController] Detected {data.GetType().Name}, Tracking started for 15s.");
        }

        private void ClearActiveHazard()
        {
            activeHazard = null;
            CurrentIntensity = 0f;
        }
    }
}
