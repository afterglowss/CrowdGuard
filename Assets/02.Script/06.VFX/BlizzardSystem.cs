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

    private Coroutine _stopCoroutine;

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

        if (duration > 0f)
            _stopCoroutine = StartCoroutine(StopAfterDuration(duration));
        // duration <= 0 : Stop()을 호출할 때까지 유지
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

        if (blizzardParticles != null)
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private IEnumerator StopAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (blizzardParticles != null)
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _stopCoroutine = null;
    }
}
