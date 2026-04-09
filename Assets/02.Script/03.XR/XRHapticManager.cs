using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace CrowdGuard.XR
{
    /// <summary>
    /// XR Gameplay Rig에 부착.
    /// 모든 시스템(RopeSystem, AnchorSystem 등)이 컨트롤러 진동을 요청할 수 있는 공용 매니저.
    /// </summary>
    public class XRHapticManager : MonoBehaviour
    {
        [Header("Global Settings")]
        [Tooltip("전체 진동 세기 배율 (0 = 진동 끔, 1 = 원본 그대로)")]
        [SerializeField][Range(0f, 1f)] private float _globalAmplitudeMultiplier = 1.0f;

        [Header("Controller References")]
        [Tooltip("왼손 컨트롤러의 HapticImpulsePlayer")]
        [SerializeField] private HapticImpulsePlayer _leftHapticPlayer;
        [Tooltip("오른손 컨트롤러의 HapticImpulsePlayer")]
        [SerializeField] private HapticImpulsePlayer _rightHapticPlayer;

        public void SendHapticToLeft(float amplitude, float duration)
        {
            if (_leftHapticPlayer != null)
                _leftHapticPlayer.SendHapticImpulse(amplitude * _globalAmplitudeMultiplier, duration);
        }

        public void SendHapticToRight(float amplitude, float duration)
        {
            if (_rightHapticPlayer != null)
                _rightHapticPlayer.SendHapticImpulse(amplitude * _globalAmplitudeMultiplier, duration);
        }

        public void SendHapticToBoth(float amplitude, float duration)
        {
            SendHapticToLeft(amplitude, duration);
            SendHapticToRight(amplitude, duration);
        }

        /// <summary>
        /// 외부에서 찾은 HapticImpulsePlayer에 글로벌 멀티플라이어를 적용하여 진동 전송.
        /// IceAxeView 등 자체적으로 HapticImpulsePlayer를 탐색하는 시스템용.
        /// </summary>
        public void SendHaptic(HapticImpulsePlayer player, float amplitude, float duration)
        {
            if (player != null)
                player.SendHapticImpulse(amplitude * _globalAmplitudeMultiplier, duration);
        }
    }
}
