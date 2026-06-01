using UnityEngine;
using SimpleAudioManager;

/// <summary>
/// XR Rig 카메라에 붙은 "Fog Snow Blizzard" 오브젝트에 부착합니다.
/// 평상시에는 Snow만 켜져 눈이 내리고, 눈보라 진입 시 Snow를 끄고 Fog+Blizzard를 켭니다.
///
/// [로컬 전용]
/// XR Rig 카메라는 로컬 플레이어에게만 생성되므로 이 인스턴스가 곧 로컬 플레이어의 것.
/// HazardManager가 LocalInstance를 통해 토글합니다.
///
/// [참조 카운트]
/// 구역형(BlizzardZone)과 이벤트형(HazardSequenceRoutine)이 동시에 와도
/// 마지막 이탈 시점에만 평상시로 복귀하도록 Enter/Exit를 카운트로 관리합니다.
///
/// [씬 세팅]
/// - snow / fog / blizzard 에 각 자식 GameObject를 연결
/// - Snow는 기본 활성, Fog/Blizzard는 기본 비활성으로 두세요 (OnEnable에서 강제 정렬됨)
/// </summary>
public class PlayerBlizzardVisual : MonoBehaviour
{
    public static PlayerBlizzardVisual LocalInstance { get; private set; }

    [Header("토글 대상 (형제 오브젝트)")]
    [SerializeField, Tooltip("평상시 내리는 눈. 기본 ON, 눈보라 시 OFF.")]
    private GameObject snow;
    [SerializeField, Tooltip("안개 Quad. 기본 OFF, 눈보라 시 ON.")]
    private GameObject fog;
    [SerializeField, Tooltip("눈보라 파티클. 기본 OFF, 눈보라 시 ON.")]
    private GameObject blizzard;

    [Header("사운드")]
    [SerializeField, Tooltip("눈보라 동안 루핑 SFX를 재생합니다.")]
    private bool playLoopingSfx = true;

    private int _refCount = 0;
    private AudioSource _sfx;

    private void OnEnable()
    {
        LocalInstance = this;
        ShowAmbient();   // 시작 상태 보장: Snow ON, Fog/Blizzard OFF
        _refCount = 0;
    }

    private void OnDisable()
    {
        if (LocalInstance == this) LocalInstance = null;
        StopSfx();
        _refCount = 0;
    }

    /// <summary>눈보라 진입(구역/이벤트 공통). 첫 진입에만 실제 전환.</summary>
    public void Enter()
    {
        _refCount++;
        if (_refCount == 1) ShowBlizzard();
    }

    /// <summary>눈보라 이탈. 마지막 이탈에만 평상시로 복귀.</summary>
    public void Exit()
    {
        _refCount = Mathf.Max(0, _refCount - 1);
        if (_refCount == 0) ShowAmbient();
    }

    private void ShowBlizzard()
    {
        if (snow != null)     snow.SetActive(false);
        if (fog != null)      fog.SetActive(true);
        if (blizzard != null) blizzard.SetActive(true);
        StartSfx();
    }

    private void ShowAmbient()
    {
        if (snow != null)     snow.SetActive(true);
        if (fog != null)      fog.SetActive(false);
        if (blizzard != null) blizzard.SetActive(false);
        StopSfx();
    }

    private void StartSfx()
    {
        if (!playLoopingSfx || AudioManager.instance == null) return;
        if (_sfx != null) return;
        _sfx = AudioManager.instance.PlaySFXLooping(AudioManager.SFXType.Blizzard, transform);
    }

    private void StopSfx()
    {
        if (_sfx != null && AudioManager.instance != null)
            AudioManager.instance.StopSFXWithFade(_sfx, 0.5f);
        _sfx = null;
    }
}
