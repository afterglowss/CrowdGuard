using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using Oculus.Haptics;

namespace CrowdGuard.XR.Haptics
{
    /// <summary>
    /// IHapticProvider 구현체. 
    /// 각 컨트롤러(Left/Right) 루트에 부착되어 진동 처리를 전담합니다.
    /// </summary>
    public class HapticProvider : MonoBehaviour, IHapticProvider
    {
        [Header("Settings")]
        [Tooltip("현재 컨트롤러의 글로벌 진동 세기 배율 (0 = 진동 끔, 1 = 원본 그대로)")]
        [SerializeField][Range(0f, 1f)] private float _globalAmplitudeMultiplier = 1.0f;

        [Header("References")]
        [Tooltip("현재 컨트롤러의 HapticImpulsePlayer (비워두면 자동 할당)")]
        [SerializeField] private HapticImpulsePlayer _impulsePlayer;

        [Tooltip("이 컨트롤러가 왼손인지 오른손인지 선택하세요.")]
        [SerializeField] private Controller _metaControllerContext = Controller.Both;

        [Tooltip("True로 두면 Meta Haptic Clip을 무시하고 XRI 기본 햅틱(Fallback)만 강제로 사용합니다.")]
        [SerializeField] private bool _forceXRIFallback = false;

        private void Awake()
        {
            if (_impulsePlayer == null)
            {
                _impulsePlayer = GetComponentInChildren<HapticImpulsePlayer>();
            }

            // 사용자가 따로 설정하지 않고 Both일 경우에만 자동 감지 시도
            if (_metaControllerContext == Controller.Both)
            {
                bool isLeft = true;
                if (gameObject.name.ToLower().Contains("right"))
                {
                    isLeft = false;
                }
                else
                {
                    var controller = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.XRBaseController>();
                    if (controller != null && controller.gameObject.name.ToLower().Contains("right"))
                    {
                        isLeft = false;
                    }
                }
                _metaControllerContext = isLeft ? Controller.Left : Controller.Right;
            }
            Debug.Log($"[HapticProvider] Awake: {gameObject.name} 초기화 완료! 방향={_metaControllerContext}, XRI-Player={(_impulsePlayer != null ? "연결됨" : "없음")}");
        }

        public void PlayHaptic(HapticProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning($"[HapticProvider] PlayHaptic 실패: 받은 프로파일이 비어있음! ({gameObject.name})");
                return;
            }

            Debug.Log($"[HapticProvider] PlayHaptic 요청 됨 ({profile.name}). 손: {_metaControllerContext}");

            if (profile.clip != null && !_forceXRIFallback)
            {
                // Meta Haptics Play
                HapticClipPlayer runtimePlayer = new HapticClipPlayer(profile.clip);
                runtimePlayer.isLooping = profile.isLooping;

                // Meta HapticClipPlayer도 amplitude 프로퍼티를 지원하므로 글로벌 배율 적용
                // (일부 버전/디바이스에서는 작동 방식이 다를 수 있으나 스펙상 지원)
                runtimePlayer.amplitude = _globalAmplitudeMultiplier;

                Debug.Log($"[HapticProvider] Meta Haptic Clip 재생! -> {profile.clip.name} (배율: {_globalAmplitudeMultiplier})");
                runtimePlayer.Play(_metaControllerContext);
            }
            else
            {
                // 클립이 없거나 강제로 XRI를 쓰도록 체크된 경우
                Debug.Log($"[HapticProvider] Meta Haptic 무시됨. 폴백(XRI)으로 우회합니다. (Clip={profile.clip!=null}, ForceXRI={_forceXRIFallback})");
                PlayHaptic(profile.fallbackAmplitude, profile.fallbackDuration);
            }
        }

        public void PlayHaptic(float amplitude, float duration)
        {
            if (_impulsePlayer != null && amplitude > 0 && duration > 0 && _globalAmplitudeMultiplier > 0)
            {
                Debug.Log($"[HapticProvider] XRI Fallback Impulse 재생! Amp:{amplitude * _globalAmplitudeMultiplier}, Dur:{duration}");
                _impulsePlayer.SendHapticImpulse(amplitude * _globalAmplitudeMultiplier, duration);
            }
            else
            {
                Debug.Log($"[HapticProvider] XRI Fallback 조건 미달로 재생 취소. (Player={_impulsePlayer!=null}, Amp={amplitude}, Dur={duration}, Multiplier={_globalAmplitudeMultiplier})");
            }
        }
    }
}
