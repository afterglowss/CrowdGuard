using UnityEngine;

/// <summary>
/// 눈보라 위치 마커. 센서가 "어디서 눈보라가 오는지" 가리킬 기준점만 제공합니다.
/// (이벤트형 눈보라 전용 — HazardManager.blizzardSystems[index]로 참조)
///
/// 실제 시각효과는 XR Rig의 PlayerBlizzardVisual이 1인칭으로 담당하므로,
/// 이 오브젝트에는 파티클이 없습니다. 씬에는 빈 오브젝트로 두면 됩니다.
/// </summary>
public class BlizzardSystem : MonoBehaviour
{
    [SerializeField, Tooltip("센서가 가리킬 실제 발생 위치. 비워두면 피봇을 사용합니다.")]
    private Transform _spawnOrigin;

    public Vector3 GetSpawnPosition() =>
        _spawnOrigin != null ? _spawnOrigin.position : transform.position;
}
