using UnityEngine;
using Oculus.Haptics;

namespace CrowdGuard.XR.Haptics
{
    /// <summary>
    /// ScriptableObject. 상황별 햅틱 프로파일.
    /// Meta Haptics SDK의 HapticClip을 우선 재생하고, 없거나 fallback 설정 시 XRI 기본 진동 사용.
    /// </summary>
    [CreateAssetMenu(fileName = "NewHapticProfile", menuName = "CrowdGuard/XR/Haptic Profile")]
    public class HapticProfile : ScriptableObject
    {
        public HapticType hapticType;

        [Header("Meta Haptic Clip (우선)")]
        [Tooltip("재생할 Meta Haptic Clip. 비워두면 폴백 진동이 재생됩니다.")]
        public HapticClip clip;
        [Tooltip("루프 재생 여부 (Ambience 등)")]
        public bool isLooping;

        [Header("XRI Fallback")]
        [Range(0f, 1f)] public float fallbackAmplitude = 0.5f;
        public float fallbackDuration = 0.2f;
    }
}