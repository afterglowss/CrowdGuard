using System.Collections;
using UnityEngine;

/// <summary>
/// 씬에 미리 배치해두는 눈보라 파티클 시스템.
/// HazardManager.blizzardSystems 리스트에 인덱스 순서로 등록하세요.
/// </summary>
public class BlizzardSystem : MonoBehaviour
{
    [SerializeField] private ParticleSystem blizzardParticles;
    [SerializeField, Tooltip("센서가 가리킬 실제 발생 위치. 비워두면 피봇을 사용합니다.")]
    private Transform _spawnOrigin;

    [Header("카메라 추적 (시야 전체를 덮는 눈보라용)")]
    [SerializeField, Tooltip("켜면 재생 중 플레이어 카메라의 '위치'만 따라갑니다. 고개 회전은 따라가지 않습니다.\nParticle System의 Simulation Space를 World로 두는 것을 권장합니다.")]
    private bool _followCameraPosition = false;
    [SerializeField, Tooltip("카메라 기준 오프셋(월드 기준). 예: 위(0, 3, 0)에서 눈을 뿌리려면 Y를 올리세요.")]
    private Vector3 _followOffset = Vector3.zero;

    private Coroutine _stopCoroutine;
    private bool _isActive = false;
    private Transform _followTarget; // 런타임에 찾는 카메라 (XR Rig가 나중에 생성됨)

    public Vector3 GetSpawnPosition() =>
        _spawnOrigin != null ? _spawnOrigin.position : transform.position;

    /// <summary>
    /// 파티클을 재생합니다.
    /// duration &gt; 0이면 해당 시간 후 자동으로 멈춥니다 (HazardTriggerZone 등 이벤트형).
    /// duration &lt;= 0이면 Stop()을 호출할 때까지 계속 재생합니다 (BlizzardZone 구역형).
    /// 이미 재생 중이면 처음부터 다시 시작합니다.
    /// </summary>
    public void Activate(float duration = 0f)
    {
        if (_stopCoroutine != null)
        {
            StopCoroutine(_stopCoroutine);
            _stopCoroutine = null;
        }

        if (blizzardParticles != null)
        {
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            blizzardParticles.Play();
        }

        _isActive = true;

        if (duration > 0f)
            _stopCoroutine = StartCoroutine(StopAfterDuration(duration));
        // duration <= 0 : Stop()을 호출할 때까지 유지
    }

    private void LateUpdate()
    {
        if (!_isActive || !_followCameraPosition) return;

        // XR Rig는 나중에 생성되므로 런타임에 지연 탐색
        if (_followTarget == null)
        {
            if (Camera.main != null) _followTarget = Camera.main.transform;
            else return;
        }

        // 위치만 따라가고 회전은 건드리지 않음 (고개 돌려도 눈보라는 월드 고정)
        transform.position = _followTarget.position + _followOffset;
    }

    /// <summary>
    /// 외부에서 강제로 멈출 때 사용합니다.
    /// </summary>
    public void Stop()
    {
        if (_stopCoroutine != null)
        {
            StopCoroutine(_stopCoroutine);
            _stopCoroutine = null;
        }

        _isActive = false;

        if (blizzardParticles != null)
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private IEnumerator StopAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        _isActive = false;
        if (blizzardParticles != null)
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _stopCoroutine = null;
    }
}
