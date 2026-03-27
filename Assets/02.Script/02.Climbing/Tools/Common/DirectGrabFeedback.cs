using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using CrowdGuard.XR;

namespace CrowdGuard.Climbing.Tools
{
    /// <summary>
    /// Direct Grab 시 Hover/Grab 피드백 (하이라이트 + 진동).
    /// XRGrabInteractable이 있는 아이템에 붙여주세요.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class DirectGrabFeedback : MonoBehaviour
    {
        [Header("Hover 피드백")]
        [Tooltip("Hover 시 활성화할 하이라이트 오브젝트 (Outline 등)")]
        [SerializeField] private GameObject _highlightObject;

        [Header("Haptics")]
        [Tooltip("XR Gameplay Rig의 XRHapticManager를 연결")]
        [SerializeField] private XRHapticManager _hapticManager;

        [Tooltip("Hover 시 진동 강도 (0~1)")]
        [SerializeField][Range(0f, 1f)] private float _hoverHapticAmplitude = 0.1f;
        [Tooltip("Hover 시 진동 지속 시간 (초)")]
        [SerializeField] private float _hoverHapticDuration = 0.05f;

        [Tooltip("Grab 시 진동 강도 (0~1)")]
        [SerializeField][Range(0f, 1f)] private float _grabHapticAmplitude = 0.4f;
        [Tooltip("Grab 시 진동 지속 시간 (초)")]
        [SerializeField] private float _grabHapticDuration = 0.1f;

        private XRGrabInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<XRGrabInteractable>();
            if (_highlightObject != null) _highlightObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.AddListener(OnHoverEnter);
                _interactable.hoverExited.AddListener(OnHoverExit);
                _interactable.selectEntered.AddListener(OnGrabbed);
            }
        }

        private void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
                _interactable.selectEntered.RemoveListener(OnGrabbed);
            }
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            // 하이라이트 켜기
            if (_highlightObject != null) _highlightObject.SetActive(true);

            // 약한 진동 (손이 근처에 왔다는 촉각 신호)
            SendHaptic(args.interactorObject, _hoverHapticAmplitude, _hoverHapticDuration);
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            if (_highlightObject != null) _highlightObject.SetActive(false);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            // 잡으면 하이라이트 끄기
            if (_highlightObject != null) _highlightObject.SetActive(false);

            // 중간 강도 진동
            SendHaptic(args.interactorObject, _grabHapticAmplitude, _grabHapticDuration);
        }

        private void SendHaptic(IXRInteractor interactor, float amplitude, float duration)
        {
            if (_hapticManager == null) return;

            var player = (interactor as MonoBehaviour)?
                .GetComponentInParent<HapticImpulsePlayer>();

            if (player != null)
            {
                _hapticManager.SendHaptic(player, amplitude, duration);
            }
        }
    }
}
