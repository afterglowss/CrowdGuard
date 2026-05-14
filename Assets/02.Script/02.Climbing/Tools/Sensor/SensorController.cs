using SimpleAudioManager;
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
        [SerializeField, Tooltip("Previous mode button (Right Controller secondaryButton / B).")]
        private InputActionReference previousModeAction;
        [SerializeField, Tooltip("Next mode button (Right Controller primaryButton / A).")]
        private InputActionReference nextModeAction;

        public SensorMode CurrentMode { get; private set; } = SensorMode.Avalanche;

        public event Action<SensorMode> OnModeChanged;
        public event Action<HazardData> OnHazardDetected; // 추적 시작 이벤트

        public float CurrentIntensity { get; private set; }
        private HazardData activeHazard;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable;
        private bool isHeld = false;
        private InputAction defaultPreviousModeAction;
        private InputAction defaultNextModeAction;

        [Header("Beep Settings")]
        [SerializeField] private float beepIntervalMax = 2.0f;  // intensity 낮을 때 (느린 삐)
        [SerializeField] private float beepIntervalMin = 0.07f; // intensity 높을 때 (빠른 삐삐삐)
        [SerializeField] private float beepThreshold   = 0.05f; // 이 이하면 무음

        private Coroutine _beepCoroutine;

        private void Awake()
        {
            interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            defaultPreviousModeAction = new InputAction(
                "Sensor Previous Mode",
                InputActionType.Button,
                "<XRController>{RightHand}/{SecondaryButton}");
            defaultNextModeAction = new InputAction(
                "Sensor Next Mode",
                InputActionType.Button,
                "<XRController>{RightHand}/{PrimaryButton}");
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
            DisableModeActions();

            try {
                HazardManager.OnHazardWarning -= HandleHazardWarning;
            } catch { }
        }

        private void OnDestroy()
        {
            defaultPreviousModeAction?.Dispose();
            defaultNextModeAction?.Dispose();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            isHeld = true;
            EnableModeActions();
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            isHeld = false;
            DisableModeActions();
        }

        private void Update()
        {
            // 오른손 B/A 버튼으로 이전/다음 모드를 순환
            if (isHeld)
            {
                if (WasModeButtonPressed(previousModeAction, defaultPreviousModeAction))
                    CycleMode(-1);

                if (WasModeButtonPressed(nextModeAction, defaultNextModeAction))
                    CycleMode(1);
            }

            // 실시간 신호 강도 추적
            if (activeHazard != null)
            {
                // 눈사태: 이동 중인 선두 위치를 실시간으로 추적
                // 그 외 재난: HazardData.Location 고정값 사용
                Vector3 targetPos;
                if (activeHazard is AvalancheData avalancheData
                    && avalancheData.PathSystem != null)
                {
                    targetPos = avalancheData.PathSystem.GetHeadPosition();
                }
                else
                {
                    targetPos = activeHazard.Location;
                }

                Vector3 diff = targetPos - transform.position;
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

        private void EnableModeActions()
        {
            EnableInputAction(previousModeAction, defaultPreviousModeAction);
            EnableInputAction(nextModeAction, defaultNextModeAction);
        }

        private void DisableModeActions()
        {
            DisableInputAction(previousModeAction, defaultPreviousModeAction);
            DisableInputAction(nextModeAction, defaultNextModeAction);
        }

        private static void EnableInputAction(InputActionReference actionReference, InputAction fallbackAction)
        {
            InputAction action = actionReference != null && actionReference.action != null
                ? actionReference.action
                : fallbackAction;

            if (action != null && !action.enabled)
                action.Enable();
        }

        private static void DisableInputAction(InputActionReference actionReference, InputAction fallbackAction)
        {
            if (actionReference != null && actionReference.action != null)
                return;

            if (fallbackAction != null && fallbackAction.enabled)
                fallbackAction.Disable();
        }

        private static bool WasModeButtonPressed(InputActionReference actionReference, InputAction fallbackAction)
        {
            InputAction action = actionReference != null && actionReference.action != null
                ? actionReference.action
                : fallbackAction;

            return action != null && action.WasPressedThisFrame();
        }

        private void CycleMode(int direction)
        {
            int modeCount = Enum.GetNames(typeof(SensorMode)).Length;
            int nextModeIndex = ((int)CurrentMode + direction + modeCount) % modeCount;
            CurrentMode = (SensorMode)nextModeIndex;

            AudioManager.instance.PlaySFX(AudioManager.SFXType.SensorSwitch, transform);

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

            AudioManager.instance.PlaySFX(AudioManager.SFXType.SensorBeep, transform);

            if (_beepCoroutine != null) StopCoroutine(_beepCoroutine);
            _beepCoroutine = StartCoroutine(BeepCoroutine());

            OnHazardDetected?.Invoke(data);
            Debug.Log($"[SensorController] Detected {data.GetType().Name}, Tracking started for 15s.");
        }

        private void ClearActiveHazard()
        {
            activeHazard = null;
            CurrentIntensity = 0f;

            if (_beepCoroutine != null)
            {
                StopCoroutine(_beepCoroutine);
                _beepCoroutine = null;
            }
        }

        private System.Collections.IEnumerator BeepCoroutine()
        {
            while (activeHazard != null)
            {
                if (CurrentIntensity > beepThreshold)
                {
                    AudioManager.instance.PlaySFX(AudioManager.SFXType.SensorBeep, transform);
                    float interval = Mathf.Lerp(beepIntervalMax, beepIntervalMin, CurrentIntensity);
                    yield return new WaitForSeconds(interval);
                }
                else
                {
                    yield return new WaitForSeconds(0.15f);
                }
            }
            _beepCoroutine = null;
        }
    }
}
