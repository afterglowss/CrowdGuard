using SimpleAudioManager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using CrowdGuard.XR.Haptics;

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
        public event Action<HazardData> OnHazardDetected;

        public float CurrentIntensity { get; private set; }

        [Header("Tracking Settings")]
        [SerializeField, Tooltip("재난 감지 후 추적 지속 시간(초).")]
        private float trackingDuration = 15f;
        public float TrackingDuration => trackingDuration;

        private HazardData activeHazard;

        // 모드 불일치 시에도 재난을 보존해두는 대기 목록
        private readonly Dictionary<SensorMode, HazardData> _pendingHazards = new();
        private readonly Dictionary<SensorMode, float>     _pendingExpiry   = new();
        private Coroutine _trackingExpiry;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable;
        private bool isHeld = false;
        private InputAction defaultPreviousModeAction;
        private InputAction defaultNextModeAction;

        [Header("Beep Settings")]
        [SerializeField] private float beepIntervalMax = 2.0f;
        [SerializeField] private float beepIntervalMin = 0.07f;
        [SerializeField] private float beepThreshold   = 0.05f;

        [Header("Haptic Settings")]
        [SerializeField] private HapticProfile sensorSwitchProfile;
        [SerializeField] private HapticProfile avalancheWarningProfile;
        [SerializeField] private HapticProfile rockfallWarningProfile;
        [SerializeField] private HapticProfile blizzardWarningProfile;

        private IHapticProvider _heldHapticProvider;
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
            try { HazardManager.OnHazardWarning += HandleHazardWarning; } catch { }
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            DisableModeActions();
            try { HazardManager.OnHazardWarning -= HandleHazardWarning; } catch { }
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

            if (args.interactorObject != null)
            {
                var t = args.interactorObject.transform;
                _heldHapticProvider = t.GetComponentInChildren<IHapticProvider>(true)
                                   ?? t.GetComponentInParent<IHapticProvider>(true);
            }
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            isHeld = false;
            DisableModeActions();
            _heldHapticProvider?.StopHaptic();
            _heldHapticProvider = null;
        }

        private void Update()
        {
            if (isHeld)
            {
                if (WasModeButtonPressed(previousModeAction, defaultPreviousModeAction)) CycleMode(-1);
                if (WasModeButtonPressed(nextModeAction,     defaultNextModeAction))     CycleMode(1);
            }

            if (activeHazard != null)
            {
                Vector3 targetPos = (activeHazard is AvalancheData av && av.PathSystem != null)
                    ? av.PathSystem.GetHeadPosition()
                    : activeHazard.Location;

                Vector3 diff = (targetPos - transform.position);
                diff.y = 0;
                diff.Normalize();
                CurrentIntensity = Mathf.Clamp01((Vector3.Dot(transform.forward, diff) + 1f) / 2f);
            }
        }

        public void MockToggleMode() => CycleMode(1);

        private void CycleMode(int direction)
        {
            int modeCount     = Enum.GetNames(typeof(SensorMode)).Length;
            int nextModeIndex = ((int)CurrentMode + direction + modeCount) % modeCount;
            CurrentMode = (SensorMode)nextModeIndex;

            AudioManager.instance.PlaySFX(AudioManager.SFXType.SensorSwitch, transform);
            if (_heldHapticProvider != null && sensorSwitchProfile != null)
                _heldHapticProvider.PlayHaptic(sensorSwitchProfile);

            Debug.Log($"[SensorController] Mode switched: {CurrentMode}");

            // 모드 전환 시 기존 추적 중단 (pending은 유지)
            StopActiveTracking();

            // OnModeChanged 먼저 → SensorView가 RefreshUI 실행
            OnModeChanged?.Invoke(CurrentMode);

            // 새 모드에 유효한 대기 재난이 있으면 즉시 전체 추적 시작
            if (_pendingHazards.TryGetValue(CurrentMode, out HazardData pending)
                && Time.time < _pendingExpiry[CurrentMode])
            {
                ActivateFullTracking(pending);
            }
        }

        public void HandleHazardWarning(HazardData data)
        {
            SensorMode hazardMode = GetHazardMode(data);
            if (hazardMode < 0) return;

            // 모드 불문 단발 경고음
            AudioManager.instance.PlaySFXNoRand(AudioManager.SFXType.SensorBeep, transform);

            // 대기 목록에 저장
            _pendingHazards[hazardMode] = data;
            _pendingExpiry[hazardMode]  = Time.time + trackingDuration;

            if (CurrentMode == hazardMode)
                ActivateFullTracking(data);
            else
                Debug.Log($"[SensorController] {hazardMode} 재난 감지 — 모드 불일치, 경고음만 재생");
        }

        private void ActivateFullTracking(HazardData data)
        {
            StopActiveTracking(); // 이전 추적 정리 (pending은 유지)
            activeHazard = data;

            SensorMode hazardMode = GetHazardMode(data);
            float remaining = _pendingExpiry.TryGetValue(hazardMode, out float expiry)
                ? expiry - Time.time
                : trackingDuration;

            // 만료 코루틴 시작
            if (_trackingExpiry != null) StopCoroutine(_trackingExpiry);
            _trackingExpiry = StartCoroutine(TrackingExpiryRoutine(hazardMode, Mathf.Max(0f, remaining)));

            // 햅틱
            if (_heldHapticProvider != null)
            {
                if (data is AvalancheData && avalancheWarningProfile != null)
                    _heldHapticProvider.PlayLoopingHaptic(avalancheWarningProfile);
                else if (data is RockfallData && rockfallWarningProfile != null)
                    _heldHapticProvider.PlayHaptic(rockfallWarningProfile);
                else if (data is BlizzardData && blizzardWarningProfile != null)
                    _heldHapticProvider.PlayLoopingHaptic(blizzardWarningProfile);
            }

            if (_beepCoroutine != null) StopCoroutine(_beepCoroutine);
            _beepCoroutine = StartCoroutine(BeepCoroutine());

            OnHazardDetected?.Invoke(data);
            Debug.Log($"[SensorController] 전체 추적 시작: {data.GetType().Name} (남은 시간: {remaining:F1}s)");
        }

        /// <summary>
        /// 현재 활성 추적만 중단합니다. pending은 유지되어 모드 전환 시 재활성화됩니다.
        /// </summary>
        private void StopActiveTracking()
        {
            activeHazard = null;
            CurrentIntensity = 0f;

            _heldHapticProvider?.StopHaptic();

            if (_beepCoroutine != null)
            {
                StopCoroutine(_beepCoroutine);
                _beepCoroutine = null;
            }
        }

        private IEnumerator TrackingExpiryRoutine(SensorMode hazardMode, float duration)
        {
            yield return new WaitForSeconds(duration);
            _pendingHazards.Remove(hazardMode);
            _pendingExpiry.Remove(hazardMode);
            StopActiveTracking();
            _trackingExpiry = null;
            Debug.Log($"[SensorController] {hazardMode} 추적 만료");
        }

        private static SensorMode GetHazardMode(HazardData data)
        {
            if (data is AvalancheData) return SensorMode.Avalanche;
            if (data is RockfallData)  return SensorMode.Rockfall;
            if (data is BlizzardData)  return SensorMode.Blizzard;
            return (SensorMode)(-1);
        }

        private IEnumerator BeepCoroutine()
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

        // ── Input 유틸 ──────────────────────────────────────────────

        private void EnableModeActions()
        {
            EnableInputAction(previousModeAction, defaultPreviousModeAction);
            EnableInputAction(nextModeAction,     defaultNextModeAction);
        }

        private void DisableModeActions()
        {
            DisableInputAction(previousModeAction, defaultPreviousModeAction);
            DisableInputAction(nextModeAction,     defaultNextModeAction);
        }

        private static void EnableInputAction(InputActionReference r, InputAction fallback)
        {
            InputAction a = (r != null && r.action != null) ? r.action : fallback;
            if (a != null && !a.enabled) a.Enable();
        }

        private static void DisableInputAction(InputActionReference r, InputAction fallback)
        {
            if (r != null && r.action != null) return;
            if (fallback != null && fallback.enabled) fallback.Disable();
        }

        private static bool WasModeButtonPressed(InputActionReference r, InputAction fallback)
        {
            InputAction a = (r != null && r.action != null) ? r.action : fallback;
            return a != null && a.WasPressedThisFrame();
        }
    }
}
