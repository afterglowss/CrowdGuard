using UnityEngine;
using TMPro;
using System.Collections;

namespace MSEX.Climbing.Tools
{
    [RequireComponent(typeof(SensorController))]
    public class SensorView : MonoBehaviour
    {
        [Header("UI Objects")]
        [SerializeField] private GameObject avalancheUI;
        [SerializeField] private GameObject rockfallUI;
        [SerializeField] private GameObject blizzardUI;

        [Header("Shared Texts")]
        [SerializeField] private TMP_Text statusText;

        [Header("Feedback Visuals")]
        [SerializeField] private RectTransform rockfallArrow;

        private SensorController controller;

        private void Awake()
        {
            controller = GetComponent<SensorController>();
        }

        private void OnEnable()
        {
            controller.OnModeChanged    += OnModeChangedHandler;
            controller.OnHazardDetected += TriggerWarningFeedback;
        }

        private void OnDisable()
        {
            controller.OnModeChanged    -= OnModeChangedHandler;
            controller.OnHazardDetected -= TriggerWarningFeedback;
        }

        private void Start()
        {
            RefreshUI(controller.CurrentMode);
        }

        /// <summary>
        /// 모드 전환 시 호출됩니다. 진행 중인 WARNING 코루틴을 중단하고 UI를 갱신합니다.
        /// </summary>
        private void OnModeChangedHandler(SensorMode newMode)
        {
            StopAllCoroutines();
            RefreshUI(newMode);
        }

        private void RefreshUI(SensorMode newMode)
        {
            if (avalancheUI != null) avalancheUI.SetActive(newMode == SensorMode.Avalanche);
            if (rockfallUI != null) rockfallUI.SetActive(newMode == SensorMode.Rockfall);
            if (blizzardUI != null) blizzardUI.SetActive(newMode == SensorMode.Blizzard);
            
            if (statusText != null)
                statusText.text = $"MODE: {newMode.ToString().ToUpper()}";
        }

        private void TriggerWarningFeedback(HazardData hazard)
        {
            if (statusText != null)
            {
                StopAllCoroutines(); // 기존 코루틴 취소 후 재추적
                StartCoroutine(WarningTextRoutine(hazard.GetType().Name));
            }
        }

        private IEnumerator WarningTextRoutine(string hazardName)
        {
            float elapsed = 0f;
            while (elapsed < controller.TrackingDuration)
            {
                elapsed += Time.deltaTime;

                if (statusText != null)
                {
                    statusText.text = $"<color=red>WARNING: {hazardName}</color>\nSignal: {controller.CurrentIntensity:P0}";
                }

                if (controller.CurrentMode == SensorMode.Rockfall && rockfallArrow != null)
                {
                    float scale = Mathf.Lerp(0.5f, 1.5f, controller.CurrentIntensity);
                    rockfallArrow.localScale = Vector3.one * scale;
                }

                yield return null; // 한 프레임 대기
            }
            
            RefreshUI(controller.CurrentMode);
        }
    }
}
